using Business.Abstract;

using Core.Aspect.Autofac.Transaction;
using Core.Utilities.Configuration;
using Core.Utilities.Helpers;
using DataAccess.Concrete;
using Entities.Concrete.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Api.BackgroundServices
{
    public class AppointmentTimeoutWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<BackgroundServicesSettings> backgroundServicesSettings,
        IOptions<AppointmentSettings> appointmentSettings,
        ILogger<AppointmentTimeoutWorker> logger
    ) : BackgroundService
    {
        private readonly BackgroundServicesSettings _settings = backgroundServicesSettings.Value;
        private readonly AppointmentSettings _appointmentSettings = appointmentSettings.Value;
        private readonly ILogger<AppointmentTimeoutWorker> _logger = logger;

        // 3'lü sistem (StoreSelection) süreleri - appsettings.json'dan okunuyor
        private int StoreSelectionTotalMinutes => _appointmentSettings.StoreSelection.TotalMinutes;

        // Otomatik tamamlama: randevu bitiş saatinden itibaren kaç dakika
        private int AutoCompleteAfterMinutes => _appointmentSettings.AutoCompleteAfterMinutes;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

                    // Per-cycle timeout: yavaş DB sorgusu bir sonraki cycle'ı bloklamasın
                    using var cycleCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    cycleCts.CancelAfter(TimeSpan.FromMinutes(2));
                    var cycleToken = cycleCts.Token;

                    var now = DateTime.UtcNow;
                    var nowTr = TimeZoneHelper.ToTurkeyTime(now);
                    const int batchSize = 50; // Her seferde 50 appointment işle

                    // Yaklaşan randevular için hatırlatıcı bildirimi (push + signalr)
                    await ProcessUpcomingAppointmentRemindersAsync(db, scope, nowTr, cycleToken);

                    // Randevu bitti ama henüz tamamlanmadı → tamamlayacak kişiye push hatırlatma
                    await ProcessCompletionReminderAsync(db, scope, nowTr, cycleToken);

                    // Bitiş saatinden 30 dakika geçmiş ama tamamlanmamış randevuları otomatik tamamla
                    await ProcessAutoCompleteAppointmentsAsync(db, scope, nowTr, cycleToken);

                    // Toplam expired appointment sayısını kontrol et
                    var totalExpiredCount = await db.Appointments
                        .CountAsync(a => a.Status == AppointmentStatus.Pending
                                      && a.PendingExpiresAt != null
                                      && a.PendingExpiresAt <= now, cycleToken);


                    // Batch'ler halinde işle
                    int processedCount = 0;
                    while (processedCount < totalExpiredCount)
                    {
                        // Bir batch al
                        var expiredBatch = await db.Appointments
                            .Where(a => a.Status == AppointmentStatus.Pending
                                     && a.PendingExpiresAt != null
                                     && a.PendingExpiresAt <= now)
                            .OrderBy(a => a.PendingExpiresAt) // En eski olanları önce işle
                            .Take(batchSize)
                            .ToListAsync(cycleToken);

                        if (!expiredBatch.Any())
                            break; // Daha fazla expired appointment yok

                        foreach (var appt in expiredBatch)
                        {
                            try
                            {
                                await ProcessExpiredAppointmentAsync(appt, scope, cycleToken);
                                processedCount++;
                            }
                            catch (OperationCanceledException) when (cycleCts.IsCancellationRequested && !stoppingToken.IsCancellationRequested)
                            {
                                _logger.LogWarning("Appointment timeout cycle exceeded 2 minute limit. Skipping remaining batch.");
                                break;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "AppointmentTimeoutWorker: Failed to process expired appointment {AppointmentId}. Skipping.", appt.Id);
                                processedCount++; // Sayacı artır ki sonsuz döngüye girmesin
                            }
                        }

                        // Cycle timeout kontrolü
                        if (cycleToken.IsCancellationRequested)
                            break;

                        // Batch işlendikten sonra kısa bir bekleme (database'e fazla yük bindirmemek için)
                        if (processedCount < totalExpiredCount)
                        {
                            await Task.Delay(TimeSpan.FromMilliseconds(100), stoppingToken);
                        }
                    }

                    await Task.Delay(TimeSpan.FromSeconds(_settings.AppointmentTimeoutWorkerIntervalSeconds), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Worker'daki beklenmeyen hata tüm host'u düşürmesin (IIS 500.30'a sebep olur)
                    _logger.LogError(ex, "AppointmentTimeoutWorker cycle failed. Retrying in 10 seconds.");
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                }
            }
        }

        private static DateTime BuildAppointmentDateTimeTr(DateOnly date, TimeSpan time)
        {
            return date.ToDateTime(TimeOnly.FromTimeSpan(time));
        }

        private async Task ProcessUpcomingAppointmentRemindersAsync(
            DatabaseContext db,
            IServiceScope scope,
            DateTime nowTr,
            CancellationToken token)
        {
            var notifySvc = scope.ServiceProvider.GetRequiredService<IAppointmentNotifyService>();

            // Hedef: başlangıca ~30 dakika kalan onaylı randevular
            var min = nowTr.AddMinutes(29);
            var max = nowTr.AddMinutes(31);

            var candidates = await db.Appointments
                .Where(a =>
                    a.Status == AppointmentStatus.Approved &&
                    a.AppointmentDate != null &&
                    a.StartTime != null)
                .ToListAsync(token);

            foreach (var appt in candidates)
            {
                try
                {
                    var startTr = BuildAppointmentDateTimeTr(appt.AppointmentDate!.Value, appt.StartTime!.Value);
                    if (startTr < min || startTr > max) continue;

                    // Aynı randevu için reminder daha önce atıldıysa tekrar atma
                    var alreadySent = await db.Notifications.AnyAsync(n =>
                        n.AppointmentId == appt.Id &&
                        n.Type == NotificationType.AppointmentReminder, token);
                    if (alreadySent) continue;

                    var recipients = new[] { appt.CustomerUserId, appt.BarberStoreUserId, appt.FreeBarberUserId }
                        .Where(x => x.HasValue)
                        .Select(x => x!.Value)
                        .Distinct()
                        .ToList();

                    if (recipients.Count == 0) continue;
                    await notifySvc.NotifyToRecipientsAsync(appt.Id, NotificationType.AppointmentReminder, recipients, actorUserId: null);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send appointment reminder for {AppointmentId}", appt.Id);
                }
            }
        }

        private async Task ProcessExpiredAppointmentAsync(
            Entities.Concrete.Entities.Appointment appt,
            IServiceScope scope,
            CancellationToken stoppingToken)
        {
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var notifySvc = scope.ServiceProvider.GetRequiredService<IAppointmentNotifyService>();
            var realtime = scope.ServiceProvider.GetRequiredService<IRealTimePublisher>();
            var appointmentDal = scope.ServiceProvider.GetRequiredService<DataAccess.Abstract.IAppointmentDal>();
            var freeBarberDal = scope.ServiceProvider.GetRequiredService<DataAccess.Abstract.IFreeBarberDal>();
            var threadDal = scope.ServiceProvider.GetRequiredService<DataAccess.Abstract.IChatThreadDal>();
            var chatService = scope.ServiceProvider.GetRequiredService<IChatService>();

            // Begin transaction to ensure atomicity of all operations
            await using var transaction = await db.Database.BeginTransactionAsync(stoppingToken);

            try
            {
                var trackedAppt = await db.Appointments
                    .FirstOrDefaultAsync(a => a.Id == appt.Id, stoppingToken);

                // ÖNEMLĐ: Status Pending değilse işleme (zaten işlenmiş demektir)
                if (trackedAppt == null || trackedAppt.Status != AppointmentStatus.Pending)
                {
                    if (trackedAppt != null && trackedAppt.Status == AppointmentStatus.Unanswered)
                    {
                        _logger.LogInformation("AppointmentTimeoutWorker: Appointment {AppointmentId} is already Unanswered, skipping processing",
                            trackedAppt.Id);
                    }
                    return;
                }

            var now = DateTime.UtcNow;
            var isStoreSelectionFlow = trackedAppt.StoreSelectionType == StoreSelectionType.StoreSelection &&
                trackedAppt.CustomerUserId.HasValue &&
                trackedAppt.FreeBarberUserId.HasValue;

            if (isStoreSelectionFlow)
            {
                var overallExpiresAt = trackedAppt.CreatedAt.AddMinutes(StoreSelectionTotalMinutes);
                if (now < overallExpiresAt)
                {
                    // Store 5dk cevap vermedi
                    if (trackedAppt.BarberStoreUserId.HasValue &&
                        trackedAppt.StoreDecision == DecisionStatus.Pending)
                    {
                        var freeBarberUserId = trackedAppt.FreeBarberUserId;
                        trackedAppt.StoreDecision = DecisionStatus.NoAnswer;
                        trackedAppt.UpdatedAt = now;
                        // StoreSelectionTimeout: sadece dükkanı seçen serbest berber — başka dükkan seçebilsin
                        var recipients = new List<Guid>();
                        if (freeBarberUserId.HasValue) recipients.Add(freeBarberUserId.Value);
                        trackedAppt.Status = AppointmentStatus.Unanswered;
                        trackedAppt.PendingExpiresAt = null;

                        ClearStoreSelectionSlot(trackedAppt);

                        await db.SaveChangesAsync(stoppingToken);
                        await UpdateThreadStoreOwnerAsync(threadDal, trackedAppt.Id, null);

                        // Commit transaction before notifications (avoids TransactionScopeAspect conflict)
                        await transaction.CommitAsync(stoppingToken);

                        await chatService.PushAppointmentThreadUpdatedAsync(trackedAppt.Id);
                        if (recipients.Count > 0)
                            await notifySvc.NotifyToRecipientsAsync(trackedAppt.Id, NotificationType.StoreSelectionTimeout, recipients, actorUserId: null);
                        await UpdateAndSendNotificationsAsync(trackedAppt, db, notifySvc, realtime, scope, stoppingToken, suppressNewAppointmentUnanswered: true);
                        return;
                    }

                    // Müşteri 30dk içinde cevap vermedi (Store onayladıktan sonra)
                    if (trackedAppt.BarberStoreUserId.HasValue &&
                        trackedAppt.StoreDecision == DecisionStatus.Approved &&
                        trackedAppt.CustomerDecision == DecisionStatus.Pending)
                    {
                        var storeOwnerUserId = trackedAppt.BarberStoreUserId;
                        var freeBarberUserId = trackedAppt.FreeBarberUserId;
                        trackedAppt.CustomerDecision = DecisionStatus.NoAnswer;
                        trackedAppt.UpdatedAt = now;
                        trackedAppt.StoreDecision = DecisionStatus.Pending;
                        trackedAppt.PendingExpiresAt = overallExpiresAt;
                        // CustomerFinalTimeout: müşteri hariç — dükkan + serbest berber operasyonel bilgilensin
                        var recipients = new List<Guid>();
                        if (storeOwnerUserId.HasValue) recipients.Add(storeOwnerUserId.Value);
                        if (freeBarberUserId.HasValue) recipients.Add(freeBarberUserId.Value);
                        trackedAppt.Status = AppointmentStatus.Unanswered;
                        trackedAppt.PendingExpiresAt = null;

                        ClearStoreSelectionSlot(trackedAppt);

                        await db.SaveChangesAsync(stoppingToken);
                        await UpdateThreadStoreOwnerAsync(threadDal, trackedAppt.Id, null);

                        // Commit transaction before notifications (avoids TransactionScopeAspect conflict)
                        await transaction.CommitAsync(stoppingToken);

                        await chatService.PushAppointmentThreadUpdatedAsync(trackedAppt.Id);
                        if (recipients.Count > 0)
                            await notifySvc.NotifyToRecipientsAsync(trackedAppt.Id, NotificationType.CustomerFinalTimeout, recipients, actorUserId: null);
                        await UpdateAndSendNotificationsAsync(trackedAppt, db, notifySvc, realtime, scope, stoppingToken, suppressNewAppointmentUnanswered: true);
                        return;
                    }
                }
            }

            UpdateAppointmentStatus(trackedAppt);

            // Katılımcılar (thread removal + appointment.updated + badge update için)
            var participantUserIds = new[] { trackedAppt.CustomerUserId, trackedAppt.BarberStoreUserId, trackedAppt.FreeBarberUserId }
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            // Cevapsız olduğunda slot kilidini kaldır (availability + unique index için)
            // ÖNEMLİ: Store bilgisini (BarberStoreUserId) SAKLIYORUZ - iptal tabında görünmeli.
            // ChairName + ManuelBarberId kartta gösterim için kalır; yalnızca ChairId/saat temizlenir.
            // UYARI: Tarih ve saat bilgileri TEMİZLENMELİ - başka randevular alınabilsin!
            Guid? storeAvailabilityPushStoreId = null;
            DateOnly? storeAvailabilityPushDate = null;
            if (trackedAppt.ChairId.HasValue && trackedAppt.StoreId.HasValue && trackedAppt.AppointmentDate.HasValue)
            {
                storeAvailabilityPushStoreId = trackedAppt.StoreId;
                storeAvailabilityPushDate = trackedAppt.AppointmentDate;
            }
            if (trackedAppt.ChairId.HasValue)
            {
                if (string.IsNullOrWhiteSpace(trackedAppt.ChairName) || !trackedAppt.ManuelBarberId.HasValue)
                {
                    var chairSnap = await db.BarberChairs.AsNoTracking()
                        .FirstOrDefaultAsync(c => c.Id == trackedAppt.ChairId.Value, stoppingToken);
                    if (chairSnap != null)
                    {
                        if (string.IsNullOrWhiteSpace(trackedAppt.ChairName))
                            trackedAppt.ChairName = chairSnap.Name;
                        if (!trackedAppt.ManuelBarberId.HasValue && chairSnap.ManuelBarberId.HasValue)
                            trackedAppt.ManuelBarberId = chairSnap.ManuelBarberId;
                    }
                }

                trackedAppt.ChairId = null;
                trackedAppt.AppointmentDate = null;
                trackedAppt.StartTime = null;
                trackedAppt.EndTime = null;
            }

                await db.SaveChangesAsync(stoppingToken);

                await ReleaseFreeBarberAsync(trackedAppt, freeBarberDal, stoppingToken);

                // Thread'i kaldır + unread count'ları sıfırla + badge update schedule et
                var thread = await threadDal.Get(t => t.AppointmentId == trackedAppt.Id);
                if (thread != null)
                {
                    thread.CustomerUnreadCount = 0;
                    thread.StoreUnreadCount = 0;
                    thread.FreeBarberUnreadCount = 0;
                    thread.UpdatedAt = DateTime.UtcNow;
                    await threadDal.Update(thread);

                    // Thread'i kaldır - cevapsız randevularda thread gösterilmemeli
                    foreach (var userId in participantUserIds)
                    {
                        try { await realtime.PushChatThreadRemovedAsync(userId, thread.Id); }
                        catch (Exception ex) { _logger.LogWarning(ex, "PushChatThreadRemoved failed for userId={UserId}, threadId={ThreadId}", userId, thread.Id); }
                    }
                }

                // Appointment listesini anlık güncelle (appointment.updated)
                // Optimizasyon: Önceden tüm Cancelled listesi çekilip FirstOrDefault yapılıyordu
                // (kullanıcı geçmişi büyüdükçe N satır yükle → 1 satır kullan). Şimdi singleAppointmentId
                // ile DB'de tek satıra iniyoruz; join pipeline'ı aynı, DTO aynı.
                foreach (var userId in participantUserIds)
                {
                    try
                    {
                        var list = await appointmentDal.GetAllAppointmentByFilter(
                            userId,
                            AppointmentFilter.Cancelled,
                            singleAppointmentId: trackedAppt.Id,
                            limit: 1);
                        var dto = list.FirstOrDefault();
                        if (dto != null)
                        {
                            await realtime.PushAppointmentUpdatedAsync(userId, dto);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "PushAppointmentUpdated failed for userId={UserId}, appointmentId={AppointmentId}", userId, trackedAppt.Id);
                    }
                }

                // Commit transaction before notifications (avoids TransactionScopeAspect conflict)
                await transaction.CommitAsync(stoppingToken);

                if (storeAvailabilityPushStoreId.HasValue && storeAvailabilityPushDate.HasValue)
                {
                    try
                    {
                        await realtime.PushStoreAvailabilityChangedAsync(storeAvailabilityPushStoreId.Value, storeAvailabilityPushDate.Value);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "PushStoreAvailabilityChanged failed for appointment {AppointmentId}", trackedAppt.Id);
                    }
                }

                await UpdateAndSendNotificationsAsync(trackedAppt, db, notifySvc, realtime, scope, stoppingToken);

            }
            catch (Exception ex)
            {
                // Rollback transaction on any error
                await transaction.RollbackAsync(stoppingToken);
                _logger.LogError(ex, "Failed to process expired appointment {AppointmentId}. Transaction rolled back.", appt.Id);
                throw;
            }
        }

        /// <summary>
        /// Marks appointment as unanswered.
        /// </summary>
        private static void UpdateAppointmentStatus(Entities.Concrete.Entities.Appointment appt)
        {
            appt.Status = AppointmentStatus.Unanswered;
            appt.PendingExpiresAt = null;
            appt.UpdatedAt = DateTime.UtcNow;

            if (appt.StoreDecision == DecisionStatus.Pending)
                appt.StoreDecision = DecisionStatus.NoAnswer;

            if (appt.FreeBarberDecision == DecisionStatus.Pending)
                appt.FreeBarberDecision = DecisionStatus.NoAnswer;

            // CustomerDecision için de NoAnswer ekle (Customer -> FreeBarber + Store senaryosunda)
            if (appt.CustomerDecision == DecisionStatus.Pending)
                appt.CustomerDecision = DecisionStatus.NoAnswer;
        }

        private static void ClearStoreSelectionSlot(Entities.Concrete.Entities.Appointment appt)
        {
            appt.BarberStoreUserId = null;
            appt.ChairId = null;
            appt.AppointmentDate = null;
            appt.StartTime = null;
            appt.EndTime = null;
            // ChairName / ManuelBarberId: geçmiş seçim bilgisi — kartta göstermek için korunur
        }

        private static async Task UpdateThreadStoreOwnerAsync(DataAccess.Abstract.IChatThreadDal threadDal, Guid appointmentId, Guid? storeOwnerUserId)
        {
            var thread = await threadDal.Get(t => t.AppointmentId == appointmentId);
            if (thread == null) return;

            thread.StoreOwnerUserId = storeOwnerUserId;
            thread.UpdatedAt = DateTime.UtcNow;
            await threadDal.Update(thread);
        }

        /// <summary>
        /// Randevu bitiş saatinden itibaren, tamamlanmamışsa tamamlayacak kişiye tek seferlik push hatırlatma gönderir.
        /// </summary>
        private async Task ProcessCompletionReminderAsync(
            DatabaseContext db,
            IServiceScope scope,
            DateTime nowTr,
            CancellationToken token)
        {
            var notifySvc = scope.ServiceProvider.GetRequiredService<IAppointmentNotifyService>();

            // EndTime geçmiş, Approved, tarih/saat dolu randevular
            var candidates = await db.Appointments
                .Where(a =>
                    a.Status == AppointmentStatus.Approved &&
                    a.AppointmentDate != null &&
                    a.EndTime != null)
                .ToListAsync(token);

            foreach (var appt in candidates)
            {
                try
                {
                    var endTr = BuildAppointmentDateTimeTr(appt.AppointmentDate!.Value, appt.EndTime!.Value);

                    // Bitiş saati henüz geçmediyse atla
                    if (nowTr < endTr) continue;

                    // Oto-tamamlama süresi geçmişse artık bu hatırlatmayı göndermemize gerek yok
                    if (nowTr >= endTr.AddMinutes(AutoCompleteAfterMinutes)) continue;

                    // Bu randevu için daha önce hatırlatma gönderildi mi?
                    var alreadySent = await db.Notifications.AnyAsync(n =>
                        n.AppointmentId == appt.Id &&
                        n.Type == NotificationType.AppointmentCompletionReminder, token);
                    if (alreadySent) continue;

                    // Kimin tamamlayacağını belirle
                    // BarberStore yoksa (CustomRequest) → FreeBarber tamamlar
                    Guid? completerId = appt.BarberStoreUserId ?? appt.FreeBarberUserId;
                    if (!completerId.HasValue) continue;

                    _logger.LogInformation(
                        "AppointmentTimeoutWorker: Sending completion reminder for appointment {AppointmentId} to user {UserId}",
                        appt.Id, completerId.Value);

                    await notifySvc.NotifyToRecipientsAsync(
                        appt.Id,
                        NotificationType.AppointmentCompletionReminder,
                        new List<Guid> { completerId.Value },
                        actorUserId: null);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "ProcessCompletionReminderAsync failed for appointment {AppointmentId}", appt.Id);
                }
            }
        }

        /// <summary>
        /// Bitiş saatinden AutoCompleteAfterMinutes dakika geçmiş, Approved durumundaki randevuları otomatik tamamlar.
        /// </summary>
        private async Task ProcessAutoCompleteAppointmentsAsync(
            DatabaseContext db,
            IServiceScope scope,
            DateTime nowTr,
            CancellationToken token)
        {
            var notifySvc = scope.ServiceProvider.GetRequiredService<IAppointmentNotifyService>();
            var freeBarberDal = scope.ServiceProvider.GetRequiredService<DataAccess.Abstract.IFreeBarberDal>();
            var appointmentDal = scope.ServiceProvider.GetRequiredService<DataAccess.Abstract.IAppointmentDal>();
            var realtime = scope.ServiceProvider.GetRequiredService<IRealTimePublisher>();
            var threadDal = scope.ServiceProvider.GetRequiredService<DataAccess.Abstract.IChatThreadDal>();
            var chatService = scope.ServiceProvider.GetRequiredService<IChatService>();

            // Approved ve tarihi+saati olan randevuları çek (LINQ'ta date+time hesabı yapılamaz)
            var candidates = await db.Appointments
                .Where(a =>
                    a.Status == AppointmentStatus.Approved &&
                    a.AppointmentDate != null &&
                    a.EndTime != null)
                .ToListAsync(token);

            foreach (var appt in candidates)
            {
                try
                {
                    // Bitiş zamanını UTC olarak hesapla (StartTime gibi aynı yöntemle)
                    var endTr = BuildAppointmentDateTimeTr(appt.AppointmentDate!.Value, appt.EndTime!.Value);

                    // Henüz oto-tamamlama zamanı gelmediyse geç
                    if (nowTr < endTr.AddMinutes(AutoCompleteAfterMinutes))
                        continue;

                    await using var transaction = await db.Database.BeginTransactionAsync(token);
                    try
                    {
                        var trackedAppt = await db.Appointments
                            .FirstOrDefaultAsync(a => a.Id == appt.Id, token);

                        // Başka bir işlem tarafından zaten değiştirildiyse atla
                        if (trackedAppt == null || trackedAppt.Status != AppointmentStatus.Approved)
                            continue;

                        _logger.LogInformation(
                            "AppointmentTimeoutWorker: Auto-completing appointment {AppointmentId} (end={EndTr}, threshold={Threshold})",
                            trackedAppt.Id, endTr, endTr.AddMinutes(AutoCompleteAfterMinutes));

                        trackedAppt.Status = AppointmentStatus.Completed;
                        trackedAppt.CompletedAt = DateTime.UtcNow;
                        trackedAppt.UpdatedAt = DateTime.UtcNow;

                        // Slot kilidini kaldır
                        if (trackedAppt.ChairId.HasValue)
                        {
                            trackedAppt.ChairId = null;
                            trackedAppt.ManuelBarberId = null;
                        }

                        await db.SaveChangesAsync(token);

                        // FreeBarber'ı serbest bırak
                        if (trackedAppt.FreeBarberUserId.HasValue)
                        {
                            var fb = await freeBarberDal.Get(x => x.FreeBarberUserId == trackedAppt.FreeBarberUserId.Value);
                            if (fb != null)
                            {
                                fb.IsAvailable = true;
                                fb.UpdatedAt = DateTime.UtcNow;
                                await freeBarberDal.Update(fb);
                            }
                        }

                        // Thread'i okundu işaretle
                        if (trackedAppt.CustomerUserId.HasValue)
                            await chatService.MarkThreadReadByAppointmentAsync(trackedAppt.CustomerUserId.Value, trackedAppt.Id);
                        if (trackedAppt.FreeBarberUserId.HasValue)
                            await chatService.MarkThreadReadByAppointmentAsync(trackedAppt.FreeBarberUserId.Value, trackedAppt.Id);
                        if (trackedAppt.BarberStoreUserId.HasValue)
                            await chatService.MarkThreadReadByAppointmentAsync(trackedAppt.BarberStoreUserId.Value, trackedAppt.Id);

                        await transaction.CommitAsync(token);

                        // Tüm katılımcılara bildir (actorUserId null → herkese gider)
                        await notifySvc.NotifyAsync(trackedAppt.Id, NotificationType.AppointmentCompleted, actorUserId: null);

                        // Katılımcılara realtime appointment.updated gönder
                        var participantUserIds = new[] { trackedAppt.CustomerUserId, trackedAppt.BarberStoreUserId, trackedAppt.FreeBarberUserId }
                            .Where(x => x.HasValue)
                            .Select(x => x!.Value)
                            .Distinct()
                            .ToList();

                        // Optimizasyon: singleAppointmentId + limit:1 ile tek satır sorgu.
                        // Önceden her auto-complete'te kullanıcının tüm Completed geçmişi yükleniyordu.
                        foreach (var userId in participantUserIds)
                        {
                            try
                            {
                                var list = await appointmentDal.GetAllAppointmentByFilter(
                                    userId,
                                    AppointmentFilter.Completed,
                                    singleAppointmentId: trackedAppt.Id,
                                    limit: 1);
                                var dto = list.FirstOrDefault();
                                if (dto != null)
                                    await realtime.PushAppointmentUpdatedAsync(userId, dto);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Auto-complete PushAppointmentUpdated failed for userId={UserId}", userId);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync(token);
                        _logger.LogError(ex, "Auto-complete failed for appointment {AppointmentId}", appt.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Auto-complete outer error for appointment {AppointmentId}", appt.Id);
                }
            }
        }

        /// <summary>
        /// FreeBarber'ı release eder (IsAvailable = true)
        /// </summary>
        private async Task ReleaseFreeBarberAsync(
            Entities.Concrete.Entities.Appointment appt,
            DataAccess.Abstract.IFreeBarberDal freeBarberDal,
            CancellationToken stoppingToken)
        {
            if (!appt.FreeBarberUserId.HasValue)
                return;

            var fb = await freeBarberDal.Get(x => x.FreeBarberUserId == appt.FreeBarberUserId.Value);
            if (fb != null)
            {
                fb.IsAvailable = true;
                fb.UpdatedAt = DateTime.UtcNow;
                await freeBarberDal.Update(fb);
            }
        }

        /// <summary>
        /// Mevcut notification'ları günceller ve yeni notification'lar gönderir
        /// </summary>
        /// <param name="suppressNewAppointmentUnanswered">
        /// StoreSelectionTimeout / CustomerFinalTimeout sonrası ek AppointmentUnanswered gönderme (müşteriye yanlışlıkla gitmesini önler).
        /// </param>
        private async Task UpdateAndSendNotificationsAsync(
            Entities.Concrete.Entities.Appointment trackedAppt,
            DatabaseContext db,
            IAppointmentNotifyService notifySvc,
            IRealTimePublisher realtime,
            IServiceScope scope,
            CancellationToken stoppingToken,
            bool suppressNewAppointmentUnanswered = false)
        {
            // ÖNEMLİ: Notification Type değişmemeli - sadece payload güncellenmeli
            // Mevcut notification'ları bul (herhangi bir type olabilir - AppointmentCreated, AppointmentApproved, vb.)
            var existingNotifications = await db.Notifications
                .Where(n => n.AppointmentId == trackedAppt.Id)
                .ToListAsync(stoppingToken);

            // Mevcut notification'ları olan kullanıcılar (bunların notification'ları güncellenecek)
            var usersWithExistingNotifications = existingNotifications.Select(n => n.UserId).Distinct().ToList();

            // DÜZELTME: Sadece OKUNMAMIŞ (isRead: false) bildirimlerin payload'u güncellensin
            // Kullanıcı zaten aksiyon aldıysa (Onayla/Reddet) ve bildirim okundu işaretliyse,
            // payload'u güncellemeye gerek yok - zaten karar verilmiş durumda
            var unreadNotifications = existingNotifications.Where(n => !n.IsRead).ToList();

            // Mevcut notification'ları güncelle: Sadece okunmamış olanların payload'daki status'u güncelle, Type değiştirme
            foreach (var notif in unreadNotifications)
            {
                await UpdateNotificationPayloadAsync(notif, trackedAppt, db, realtime, stoppingToken);
            }

            if (unreadNotifications.Count > 0)
                await db.SaveChangesAsync(stoppingToken);

            var allParticipantUserIds = new[] { trackedAppt.CustomerUserId, trackedAppt.BarberStoreUserId, trackedAppt.FreeBarberUserId }
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            // AppointmentUnanswered veya aynı randevu için zaten timeout bildirimi alan kullanıcılar — çift bildirim engeli
            var usersAlreadyNotifiedTimeout = await db.Notifications
                .Where(n => n.AppointmentId == trackedAppt.Id &&
                    (n.Type == NotificationType.AppointmentUnanswered ||
                     n.Type == NotificationType.StoreSelectionTimeout ||
                     n.Type == NotificationType.CustomerFinalTimeout))
                .Select(n => n.UserId)
                .Distinct()
                .ToListAsync(stoppingToken);

            var usersWithoutNotifications = allParticipantUserIds
                .Where(userId => !usersAlreadyNotifiedTimeout.Contains(userId))
                .ToList();

            if (!suppressNewAppointmentUnanswered &&
                usersWithoutNotifications.Any() &&
                trackedAppt.Status == AppointmentStatus.Unanswered)
            {
                await SendNewUnansweredNotificationsAsync(trackedAppt, notifySvc, usersWithoutNotifications, stoppingToken);

                // NOT: MarkThreadReadByAppointmentSystemAsync çağrısı kaldırıldı
                // Çünkü thread unread count'ları zaten yukarıda sıfırlanıyor (satır 243-245)
                // Ve bu fonksiyon bildirimleri okunmuş yapabilir (istenmeyen davranış)
            }
        }

        /// <summary>
        /// Notification payload'ını günceller ve SignalR ile push eder
        /// </summary>
        private async Task UpdateNotificationPayloadAsync(
            Entities.Concrete.Entities.Notification notif,
            Entities.Concrete.Entities.Appointment trackedAppt,
            DatabaseContext db,
            IRealTimePublisher realtime,
            CancellationToken stoppingToken)
        {
            // Payload'daki status'u güncelle (veri tutarlılığı için)
            if (string.IsNullOrEmpty(notif.PayloadJson) || notif.PayloadJson.Trim() == "{}")
                return;

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                };

                // Mevcut payload'ı parse et ve status'u güncelle
                using var doc = JsonDocument.Parse(notif.PayloadJson);
                var root = doc.RootElement;

                // Yeni bir dictionary oluştur (object tipinde değerler için)
                var payloadDict = new Dictionary<string, object?>();

                // Mevcut tüm property'leri kopyala (status hariç)
                foreach (var prop in root.EnumerateObject())
                {
                    if (prop.Name.Equals("status", StringComparison.OrdinalIgnoreCase) ||
                        prop.Name.Equals("storeDecision", StringComparison.OrdinalIgnoreCase) ||
                        prop.Name.Equals("freeBarberDecision", StringComparison.OrdinalIgnoreCase) ||
                        prop.Name.Equals("customerDecision", StringComparison.OrdinalIgnoreCase) ||
                        prop.Name.Equals("pendingExpiresAt", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Value'yü object'e çevir (basit tipler için)
                    payloadDict[prop.Name] = prop.Value.ValueKind switch
                    {
                        System.Text.Json.JsonValueKind.String => prop.Value.GetString(),
                        System.Text.Json.JsonValueKind.Number => prop.Value.TryGetInt32(out var intVal) ? (object)intVal : prop.Value.GetDecimal(),
                        System.Text.Json.JsonValueKind.True => true,
                        System.Text.Json.JsonValueKind.False => false,
                        System.Text.Json.JsonValueKind.Null => null,
                        System.Text.Json.JsonValueKind.Object => JsonSerializer.Deserialize<object>(prop.Value.GetRawText()),
                        System.Text.Json.JsonValueKind.Array => JsonSerializer.Deserialize<object[]>(prop.Value.GetRawText()),
                        _ => prop.Value.GetRawText() // Complex types için raw text
                    };
                }

                // Update status and decisions
                payloadDict["status"] = (int)trackedAppt.Status;
                payloadDict["storeDecision"] = trackedAppt.StoreDecision.HasValue ? (int)trackedAppt.StoreDecision.Value : null;
                payloadDict["freeBarberDecision"] = trackedAppt.FreeBarberDecision.HasValue ? (int)trackedAppt.FreeBarberDecision.Value : null;
                payloadDict["customerDecision"] = trackedAppt.CustomerDecision.HasValue ? (int)trackedAppt.CustomerDecision.Value : null;
                payloadDict["pendingExpiresAt"] = trackedAppt.PendingExpiresAt;

                // Geri JSON string'e çevir
                notif.PayloadJson = JsonSerializer.Serialize(payloadDict, options);

                // ÖNEMLİ: Notification'ı DbContext'e attach et veya Update çağrısı yap
                // DbContext tarafından track edilmesi için
                db.Notifications.Update(notif);
            }
            catch (Exception ex)
            {
                // Payload parse edilemezse log ve devam et
                _logger.LogWarning(ex, "Failed to update notification payload for notification {NotificationId}", notif.Id);
                return;
            }

            // Güncellenmiş notification'ı SignalR ile push et (veri tutarlılığı için)
            try
            {
                var updatedDto = new Entities.Concrete.Dto.NotificationDto
                {
                    Id = notif.Id,
                    Type = notif.Type, // Type değişmedi - aynı kaldı
                    AppointmentId = notif.AppointmentId,
                    Title = notif.Title,
                    Body = notif.Body,
                    PayloadJson = notif.PayloadJson,
                    CreatedAt = notif.CreatedAt,
                    IsRead = notif.IsRead
                };
                await realtime.PushNotificationSilentUpdateAsync(notif.UserId, updatedDto);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to push updated notification {NotificationId} to SignalR", notif.Id);
            }
        }

        /// <summary>
        /// Yeni AppointmentUnanswered notification'ları gönderir
        /// </summary>
        private async Task SendNewUnansweredNotificationsAsync(
            Entities.Concrete.Entities.Appointment trackedAppt,
            IAppointmentNotifyService notifySvc,
            List<Guid> usersWithoutNotifications,
            CancellationToken stoppingToken)
        {
            try
            {
                // ÖNEMLĐ: Eğer liste boşsa bildirim gönderme (duplicate engellemek için)
                if (usersWithoutNotifications == null || !usersWithoutNotifications.Any())
                {
                    _logger.LogInformation("AppointmentTimeoutWorker: No users without notifications for appointment {AppointmentId}, skipping notification send",
                        trackedAppt.Id);
                    return;
                }

                _logger.LogInformation("AppointmentTimeoutWorker: Sending new AppointmentUnanswered notifications to {Count} users without existing notifications for appointment {AppointmentId}",
                    usersWithoutNotifications.Count, trackedAppt.Id);

                // DÜZELTME: NotifyWithAppointmentToRecipientsAsync kullan - appointment entity'sini direkt al
                // NotifyToRecipientsAsync DB'den appointment'ı tekrar okur; açık transaction içinde
                // uncommitted Unanswered status yeni bağlantıda görünmeyebilir (READ COMMITTED isolation).
                // Entity direkt geçilerek hem doğru status hem de gereksiz DB okuma önlenir.
                await notifySvc.NotifyWithAppointmentToRecipientsAsync(
                    trackedAppt,
                    NotificationType.AppointmentUnanswered,
                    usersWithoutNotifications,
                    actorUserId: null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send AppointmentUnanswered notifications for appointment {AppointmentId}", trackedAppt.Id);
                // Notification gönderimi başarısız olsa bile appointment update'i commit edilmeli
            }
        }
    }
}
