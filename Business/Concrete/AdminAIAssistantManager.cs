using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Business.Abstract;
using Business.Resources;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Business.Concrete
{
    public class AdminAIAssistantManager(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IAdminSearchService adminSearchService,
        IUserDal userDal,
        IBarberStoreDal barberStoreDal,
        IFreeBarberDal freeBarberDal,
        IBarberStoreService barberStoreService,
        IFreeBarberService freeBarberService,
        IComplaintService complaintService,
        IRequestService requestService,
        IAppointmentService appointmentService,
        IRatingService ratingService,
        IAuditService auditService,
        ILogger<AdminAIAssistantManager> logger) : IAdminAIAssistantService
    {
        // Gemini'nin OpenAI uyumlu sohbet uç noktası — function calling destekler.
        // Ücretsiz tier kullanılır (mobil tarafla aynı Gemini:ApiKey).
        private const string GeminiChatCompletionsUrl =
            "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";

        private const int MaxToolRounds = 8;
        private const int MaxHistoryMessages = 20;

        private static readonly HashSet<string> ConfirmationRequiredTools = new(StringComparer.OrdinalIgnoreCase)
        {
            "ban_user", "unban_user", "set_subscription",
            "suspend_barber_store", "suspend_free_barber",
            "resolve_complaint", "mark_request_processed",
            "cancel_appointment", "delete_rating"
        };

        private static readonly JsonSerializerOptions GeminiJsonOpts = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public async Task<IDataResult<AdminAIChatResponseDto>> ChatAsync(Guid adminId, AdminAIChatRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request?.Message))
                return new ErrorDataResult<AdminAIChatResponseDto>(Messages.AdminAiEmptyMessage);

            // Admin için ayrı kota istenirse Gemini:AdminApiKey kullanılır; yoksa mobil ile ortak Gemini:ApiKey.
            var apiKey = configuration["Gemini:AdminApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
                apiKey = configuration["Gemini:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                logger.LogError("[AdminAI] Gemini API key missing — admin asistan Gemini (ücretsiz tier) kullanır.");
                return new ErrorDataResult<AdminAIChatResponseDto>(Messages.AdminAiUnavailable);
            }

            var maxTokens = configuration.GetValue("Gemini:MaxTokens", 2048);
            const string providerLabel = "Gemini";

            var actionsExecuted = new List<AdminAIActionResultDto>();
            var messages = BuildInitialMessages(request);

            try
            {
                for (var round = 0; round < MaxToolRounds; round++)
                {
                    var response = await CallGeminiAsync(apiKey, ResolveGeminiModel(), maxTokens, messages);

                    if (response == null)
                        return new ErrorDataResult<AdminAIChatResponseDto>(Messages.AdminAiError);

                    if (response.ToolCalls.Count == 0)
                    {
                        var reply = string.IsNullOrWhiteSpace(response.Text)
                            ? Messages.AdminAiNoReply
                            : response.Text.Trim();

                        await auditService.RecordAsync(
                            AuditAction.AdminAiChatCompleted, adminId, null, null, true);

                        return new SuccessDataResult<AdminAIChatResponseDto>(new AdminAIChatResponseDto
                        {
                            Reply = reply,
                            ProviderUsed = providerLabel,
                            ActionsExecuted = actionsExecuted
                        });
                    }

                    var pendingActions = new List<AdminAIPendingActionDto>();
                    var toolResultMessages = new List<object>();

                    foreach (var tc in response.ToolCalls)
                    {
                        if (RequiresConfirmation(tc.Name))
                        {
                            var input = ParseToolInput(tc.ArgumentsJson);
                            pendingActions.Add(new AdminAIPendingActionDto
                            {
                                Id = Guid.NewGuid().ToString("N"),
                                Tool = tc.Name,
                                Summary = BuildPendingSummary(tc.Name, input),
                                InputJson = tc.ArgumentsJson
                            });
                            continue;
                        }

                        var inputEl = ParseToolInput(tc.ArgumentsJson);
                        var resultJson = await ExecuteToolAsync(adminId, tc.Name, inputEl, actionsExecuted);
                        // OpenAI uyumlu tool sonucu mesajı
                        toolResultMessages.Add(new
                        {
                            role = "tool",
                            tool_call_id = tc.Id,
                            content = resultJson
                        });
                    }

                    // Asistanın tool çağrılarını içeren mesajı ekle, ardından tool sonuçlarını
                    messages.Add(BuildAssistantMessage(response));
                    foreach (var trm in toolResultMessages)
                        messages.Add(trm);

                    if (pendingActions.Count > 0)
                    {
                        var reply = string.IsNullOrWhiteSpace(response.Text)
                            ? "Aşağıdaki işlemler için onayınız gerekiyor. Lütfen paneldeki pencereden onaylayın veya vazgeçin."
                            : response.Text.Trim() + "\n\n⚠️ Onayınız beklenen işlemler var — lütfen açılan pencereden onaylayın.";

                        return new SuccessDataResult<AdminAIChatResponseDto>(new AdminAIChatResponseDto
                        {
                            Reply = reply,
                            ProviderUsed = providerLabel,
                            ActionsExecuted = actionsExecuted,
                            RequiresConfirmation = true,
                            PendingActions = pendingActions
                        });
                    }
                }

                return new ErrorDataResult<AdminAIChatResponseDto>(Messages.AdminAiToolLimit);
            }
            catch (LlmRateLimitException)
            {
                return new ErrorDataResult<AdminAIChatResponseDto>(Messages.AdminAiRateLimit);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[AdminAI] Chat failed for adminId={AdminId}", adminId);
                return new ErrorDataResult<AdminAIChatResponseDto>(Messages.AdminAiError);
            }
        }

        public async Task<IDataResult<AdminAIChatResponseDto>> ConfirmActionsAsync(
            Guid adminId, AdminAIConfirmRequestDto request)
        {
            if (request?.Actions == null || request.Actions.Count == 0)
                return new ErrorDataResult<AdminAIChatResponseDto>(Messages.AdminAiConfirmEmpty);

            var actionsExecuted = new List<AdminAIActionResultDto>();
            var lines = new List<string>();

            foreach (var action in request.Actions)
            {
                if (string.IsNullOrWhiteSpace(action.Tool) || string.IsNullOrWhiteSpace(action.InputJson))
                    continue;

                var input = ParseToolInput(action.InputJson);
                var resultJson = await ExecuteToolAsync(adminId, action.Tool, input, actionsExecuted);
                using var doc = JsonDocument.Parse(resultJson);
                var msg = doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : action.Summary;
                lines.Add($"• {action.Summary}: {msg}");
            }

            await auditService.RecordAsync(AuditAction.AdminAiChatCompleted, adminId, null, null, true);

            var reply = lines.Count > 0
                ? Messages.AdminAiConfirmSuccess + "\n" + string.Join("\n", lines)
                : Messages.AdminAiConfirmSuccess;

            return new SuccessDataResult<AdminAIChatResponseDto>(new AdminAIChatResponseDto
            {
                Reply = reply,
                ActionsExecuted = actionsExecuted
            });
        }

        private string ResolveGeminiModel()
        {
            var configured = configuration["Gemini:Model"];
            return string.IsNullOrWhiteSpace(configured) ? "gemini-2.5-flash" : configured;
        }

        private static bool RequiresConfirmation(string? toolName) =>
            !string.IsNullOrWhiteSpace(toolName) && ConfirmationRequiredTools.Contains(toolName);

        private static List<object> BuildInitialMessages(AdminAIChatRequestDto request)
        {
            var list = new List<object>();

            if (request.History != null)
            {
                foreach (var h in request.History.TakeLast(MaxHistoryMessages))
                {
                    var role = h.Role?.ToLowerInvariant() == "assistant" ? "assistant" : "user";
                    if (!string.IsNullOrWhiteSpace(h.Content))
                        list.Add(new { role, content = h.Content.Trim() });
                }
            }

            list.Add(new { role = "user", content = request.Message.Trim() });
            return list;
        }

        private static string BuildSystemPrompt() =>
            """
            Sen Gümüş Makas yönetim paneli için Türkçe konuşan bir admin asistanısın.
            Görevin: admin'in doğal dil isteklerini anlayıp uygun aracı (function) çağırmak.

            Kurallar:
            - 6 haneli numara (ör. "217-017", "217 bin 017", "217017") doğrudan ban_user/suspend_* gibi araçlara user_id/store_id olarak verilebilir; araç numarayı kendi çözer. ID/numara verilmişse boşuna search yapma, doğrudan işlemi çağır.
            - Numara/isim belirsizse veya birden fazla aday varsa önce search_entities ile ara; tahmin etme.
            - Admin "X sayfasını göster", "son şikayetleri/talepleri/kullanıcıları göster" gibi bir görüntüleme isterse open_page aracını uygun page ile çağır (ör. complaints, users, requests). Veriyi de özetleyebilirsin.
            - Ban, askıya alma, iptal gibi yıkıcı işlemlerde sebep varsa kaydet.
            - Yıkıcı işlemler panelde admin onayından geçer; yine de doğru aracı çağır ve kullanıcıya ne yapılacağını açıkla.
            - Abonelik gate şu an KAPALI (Subscription:GateEnabled=false); set_subscription tarihi günceller ama mobil erişimi kilitlemez.
            - Askıya alma veritabanına yazılır; mobilde henüz tam enforcement olmayabilir.
            - İşlem başarısız olursa tool sonucundaki hatayı açıkla; uydurma başarı mesajı verme.
            - Kısa, net, profesyonel Türkçe yanıt ver.
            """;

        private static object[] BuildGeminiToolDefinitions() =>
        [
            AiTool("search_entities", "Panel varlıklarını ara (isim, telefon veya 6 haneli numara — '217-017' gibi yazımlar otomatik normalize edilir)",
                Props(
                    ("query", "string", "Arama metni", true),
                    ("kind", "string", "User|Store|FreeBarber|Complaint|Request|Appointment vb.", false),
                    ("limit", "integer", "Maks sonuç (1-25)", false))),
            AiTool("open_page", "Admin panelinde bir sayfayı aç/göster (kullanıcı 'X sayfasını göster', 'son şikayetleri göster' derse)",
                Props(("page", "string", "users|appointments|complaints|requests|barberstores|free-barbers|ratings|favorites|chat|earnings|manuel-barbers|service-offerings|service-packages|blocked|audit-logs|notifications|categories|map|schedule|file-manager|dashboard", true))),
            AiTool("ban_user", "Kullanıcıyı engelle",
                Props(("user_id", "string", "Kullanıcı GUID veya 6 haneli numara", true), ("reason", "string", "Sebep", false))),
            AiTool("unban_user", "Engeli kaldır", Props(("user_id", "string", "Kullanıcı GUID veya 6 haneli numara", true))),
            AiTool("set_subscription", "Abonelik bitiş tarihini güncelle",
                Props(("user_id", "string", "Kullanıcı GUID veya 6 haneli numara", true), ("end_date", "string", "ISO-8601 tarih", true))),
            AiTool("suspend_barber_store", "Dükkanı askıya al/kaldır",
                Props(
                    ("store_id", "string", "Store GUID veya salon numarası", true),
                    ("suspend", "boolean", "true=askı, false=kaldır", false),
                    ("reason", "string", "Sebep", false))),
            AiTool("suspend_free_barber", "Serbest berber askısı",
                Props(
                    ("free_barber_id", "string", "Panel GUID veya 6 haneli numara", true),
                    ("suspend", "boolean", "true=askı", false),
                    ("reason", "string", "Sebep", false))),
            AiTool("resolve_complaint", "Şikayeti çöz", Props(("complaint_id", "string", "Şikayet GUID", true))),
            AiTool("mark_request_processed", "Talebi işaretle",
                Props(
                    ("request_id", "string", "Talep GUID", true),
                    ("is_processed", "boolean", "true=işlendi", true))),
            AiTool("cancel_appointment", "Randevu iptal", Props(
                ("appointment_id", "string", "Randevu GUID", true),
                ("reason", "string", "Sebep", false))),
            AiTool("delete_rating", "Puan sil", Props(("rating_id", "string", "Puan GUID", true)))
        ];

        // OpenAI uyumlu function tanımı: { type:"function", function:{ name, description, parameters } }
        private static object AiTool(string name, string description, object inputSchema) => new
        {
            type = "function",
            function = new
            {
                name,
                description,
                parameters = inputSchema
            }
        };

        private static object Props(params (string name, string type, string desc, bool required)[] fields)
        {
            var properties = new Dictionary<string, object>();
            var required = new List<string>();
            foreach (var (name, type, desc, isReq) in fields)
            {
                properties[name] = new { type, description = desc };
                if (isReq) required.Add(name);
            }
            return new { type = "object", properties, required };
        }

        private async Task<LlmTurnResponse?> CallGeminiAsync(
            string apiKey,
            string model,
            int maxTokens,
            List<object> messages)
        {
            // OpenAI uyumlu istek: system mesajı listenin başına eklenir.
            var fullMessages = new List<object>
            {
                new { role = "system", content = BuildSystemPrompt() }
            };
            fullMessages.AddRange(messages);

            var body = new
            {
                model,
                max_tokens = maxTokens,
                temperature = 0.15,
                tools = BuildGeminiToolDefinitions(),
                tool_choice = "auto",
                messages = fullMessages
            };

            var client = httpClientFactory.CreateClient("AI");
            using var req = new HttpRequestMessage(HttpMethod.Post, GeminiChatCompletionsUrl);
            req.Headers.Add("Authorization", $"Bearer {apiKey}");
            req.Content = new StringContent(
                JsonSerializer.Serialize(body, GeminiJsonOpts), Encoding.UTF8, "application/json");

            using var httpResponse = await client.SendAsync(req);
            var raw = await httpResponse.Content.ReadAsStringAsync();

            var isQuota = raw.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase) ||
                          raw.Contains("quota", StringComparison.OrdinalIgnoreCase);
            if (httpResponse.StatusCode == HttpStatusCode.TooManyRequests || isQuota)
                throw new LlmRateLimitException();

            if (!httpResponse.IsSuccessStatusCode)
            {
                logger.LogError("[AdminAI] Gemini error {Status}: {Body}", httpResponse.StatusCode, raw);
                return null;
            }

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            if (!root.TryGetProperty("choices", out var choices) ||
                choices.ValueKind != JsonValueKind.Array ||
                choices.GetArrayLength() == 0)
                return new LlmTurnResponse();

            var message = choices[0].GetProperty("message");
            string? text = message.TryGetProperty("content", out var contentEl) &&
                           contentEl.ValueKind == JsonValueKind.String
                ? contentEl.GetString()
                : null;

            var toolCalls = new List<LlmToolCall>();
            if (message.TryGetProperty("tool_calls", out var tcArr) && tcArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var tc in tcArr.EnumerateArray())
                {
                    var fn = tc.GetProperty("function");
                    toolCalls.Add(new LlmToolCall
                    {
                        Id = tc.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String
                            ? idEl.GetString()!
                            : Guid.NewGuid().ToString("N"),
                        Name = fn.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "",
                        ArgumentsJson = fn.TryGetProperty("arguments", out var argEl) &&
                                        argEl.ValueKind == JsonValueKind.String
                            ? (argEl.GetString() ?? "{}")
                            : "{}"
                    });
                }
            }

            return new LlmTurnResponse { Text = text, ToolCalls = toolCalls };
        }

        // OpenAI uyumlu asistan mesajı: tool çağrılarını tool_calls dizisinde taşır.
        private static object BuildAssistantMessage(LlmTurnResponse response)
        {
            var toolCalls = response.ToolCalls.Select(tc => new
            {
                id = tc.Id,
                type = "function",
                function = new
                {
                    name = tc.Name,
                    arguments = string.IsNullOrWhiteSpace(tc.ArgumentsJson) ? "{}" : tc.ArgumentsJson
                }
            }).ToList();

            return new
            {
                role = "assistant",
                content = string.IsNullOrWhiteSpace(response.Text) ? null : response.Text,
                tool_calls = toolCalls
            };
        }

        private static JsonElement ParseToolInput(string argumentsJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
                return doc.RootElement.Clone();
            }
            catch
            {
                return JsonDocument.Parse("{}").RootElement.Clone();
            }
        }

        private static string BuildPendingSummary(string tool, JsonElement input) => tool switch
        {
            "ban_user" => $"Kullanıcı engellenecek (id={GetString(input, "user_id")})" +
                          (string.IsNullOrWhiteSpace(GetString(input, "reason")) ? "" : $" — Sebep: {GetString(input, "reason")}"),
            "unban_user" => $"Kullanıcı engeli kaldırılacak (id={GetString(input, "user_id")})",
            "set_subscription" => $"Abonelik bitişi güncellenecek (kullanıcı={GetString(input, "user_id")}, tarih={GetString(input, "end_date")})",
            "suspend_barber_store" => (GetBool(input, "suspend") ?? true)
                ? $"Dükkan askıya alınacak (id={GetString(input, "store_id")})"
                : $"Dükkan askısı kaldırılacak (id={GetString(input, "store_id")})",
            "suspend_free_barber" => (GetBool(input, "suspend") ?? true)
                ? $"Serbest berber askıya alınacak (id={GetString(input, "free_barber_id")})"
                : $"Serbest berber askısı kaldırılacak (id={GetString(input, "free_barber_id")})",
            "resolve_complaint" => $"Şikayet çözümlenecek (id={GetString(input, "complaint_id")})",
            "mark_request_processed" => $"Talep işaretlenecek (id={GetString(input, "request_id")}, işlendi={GetBool(input, "is_processed")})",
            "cancel_appointment" => $"Randevu iptal edilecek (id={GetString(input, "appointment_id")})",
            "delete_rating" => $"Puan silinecek (id={GetString(input, "rating_id")})",
            _ => $"{tool} uygulanacak"
        };

        private async Task<string> ExecuteToolAsync(
            Guid adminId,
            string toolName,
            JsonElement input,
            List<AdminAIActionResultDto> actionsExecuted)
        {
            try
            {
                var (success, summary) = toolName switch
                {
                    "search_entities" => await ToolSearchAsync(input),
                    "open_page" => ToolOpenPage(input),
                    "ban_user" => await ToolBanUserAsync(adminId, input),
                    "unban_user" => await ToolUnbanUserAsync(adminId, input),
                    "set_subscription" => await ToolSetSubscriptionAsync(adminId, input),
                    "suspend_barber_store" => await ToolSuspendStoreAsync(adminId, input),
                    "suspend_free_barber" => await ToolSuspendFreeBarberAsync(adminId, input),
                    "resolve_complaint" => await ToolResolveComplaintAsync(adminId, input),
                    "mark_request_processed" => await ToolMarkRequestAsync(adminId, input),
                    "cancel_appointment" => await ToolCancelAppointmentAsync(adminId, input),
                    "delete_rating" => await ToolDeleteRatingAsync(adminId, input),
                    _ => (false, $"Bilinmeyen araç: {toolName}")
                };

                actionsExecuted.Add(new AdminAIActionResultDto
                {
                    Tool = toolName,
                    Success = success,
                    Summary = summary
                });

                return JsonSerializer.Serialize(new { success, message = summary });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[AdminAI] Tool {Tool} failed", toolName);
                var err = ex.Message;
                actionsExecuted.Add(new AdminAIActionResultDto { Tool = toolName, Success = false, Summary = err });
                return JsonSerializer.Serialize(new { success = false, message = err });
            }
        }

        // page anahtarını admin paneli rotasına çevirir
        private static readonly Dictionary<string, string> PageRoutes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["dashboard"] = "/", ["users"] = "/users", ["appointments"] = "/appointments",
            ["schedule"] = "/schedule", ["map"] = "/map", ["free-barbers"] = "/free-barbers",
            ["earnings"] = "/earnings", ["barberstores"] = "/barberstores", ["chairs"] = "/chairs",
            ["service-offerings"] = "/service-offerings", ["service-packages"] = "/service-packages",
            ["manuel-barbers"] = "/manuel-barbers", ["categories"] = "/categories",
            ["complaints"] = "/complaints", ["requests"] = "/requests", ["blocked"] = "/blocked",
            ["notifications"] = "/notifications", ["audit-logs"] = "/audit-logs",
            ["ratings"] = "/ratings", ["favorites"] = "/favorites", ["chat"] = "/chat",
            ["file-manager"] = "/file-manager", ["saved-filters"] = "/saved-filters",
        };

        private static (bool, string) ToolOpenPage(JsonElement input)
        {
            var page = (GetString(input, "page") ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(page))
                return (false, "Hangi sayfanın açılacağı belirtilmedi.");
            if (!PageRoutes.TryGetValue(page, out var route))
                return (false, $"Bilinmeyen sayfa: {page}");
            // Summary = rota; frontend bunu yakalayıp yönlendirir.
            return (true, route);
        }

        private async Task<(bool, string)> ToolSearchAsync(JsonElement input)
        {
            var query = GetString(input, "query");
            if (string.IsNullOrWhiteSpace(query))
                return (false, "Arama metni gerekli.");

            var kind = GetString(input, "kind");
            var limit = Math.Clamp(GetInt(input, "limit") ?? 10, 1, 25);
            var kindArg = string.IsNullOrWhiteSpace(kind) ? null : kind;

            var result = await adminSearchService.SearchAsync(query, limit, kindArg);
            if (!result.Success || result.Data == null)
                return (false, result.Message ?? "Arama başarısız oldu.");

            if (result.Data.Count > 0)
            {
                var lines = result.Data.Select(r =>
                    $"[{r.Kind}] {r.Title} (id={r.EntityId})" + (string.IsNullOrWhiteSpace(r.Subtitle) ? "" : $" — {r.Subtitle}"));
                return (true, string.Join("\n", lines));
            }

            // Tam sonuç yoksa: kelimelere bölüp benzer sonuçlar dene (fuzzy fallback)
            var tokens = query.Split(new[] { ' ', '-', ',', '.' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length >= 3)
                .Distinct()
                .Take(4)
                .ToList();

            var near = new List<AdminSearchResultDto>();
            var seen = new HashSet<Guid>();
            foreach (var token in tokens)
            {
                var partial = await adminSearchService.SearchAsync(token, limit, kindArg);
                if (partial.Success && partial.Data != null)
                    foreach (var r in partial.Data)
                        if (seen.Add(r.EntityId)) near.Add(r);
                if (near.Count >= limit) break;
            }

            if (near.Count == 0)
                return (true, "Sonuç bulunamadı. Daha kısa/farklı bir arama terimi deneyin.");

            var nearLines = near.Take(limit).Select(r =>
                $"[{r.Kind}] {r.Title} (id={r.EntityId})" + (string.IsNullOrWhiteSpace(r.Subtitle) ? "" : $" — {r.Subtitle}"));
            return (true, "Tam eşleşme yok; benzer sonuçlar:\n" + string.Join("\n", nearLines));
        }

        private async Task<(bool, string)> ToolBanUserAsync(Guid adminId, JsonElement input)
        {
            var user = await ResolveUserAsync(input, "user_id");
            if (user == null) return (false, "Kullanıcı bulunamadı (GUID veya 6 haneli numara girin).");

            user.IsBanned = true;
            user.BanReason = GetString(input, "reason");
            user.UpdatedAt = DateTime.UtcNow;
            await userDal.Update(user);
            await auditService.RecordAsync(AuditAction.AdminUserBanned, adminId, user.Id, null, true);
            return (true, Messages.UserBannedSuccess);
        }

        private async Task<(bool, string)> ToolUnbanUserAsync(Guid adminId, JsonElement input)
        {
            var user = await ResolveUserAsync(input, "user_id");
            if (user == null) return (false, "Kullanıcı bulunamadı (GUID veya 6 haneli numara girin).");

            user.IsBanned = false;
            user.BanReason = null;
            user.UpdatedAt = DateTime.UtcNow;
            await userDal.Update(user);
            await auditService.RecordAsync(AuditAction.AdminUserUnbanned, adminId, user.Id, null, true);
            return (true, Messages.UserUnbannedSuccess);
        }

        private async Task<(bool, string)> ToolSetSubscriptionAsync(Guid adminId, JsonElement input)
        {
            var endStr = GetString(input, "end_date");
            if (string.IsNullOrWhiteSpace(endStr) || !DateTime.TryParse(endStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var endDate))
                return (false, "Geçersiz bitiş tarihi (end_date).");

            var user = await ResolveUserAsync(input, "user_id");
            if (user == null) return (false, "Kullanıcı bulunamadı (GUID veya 6 haneli numara girin).");

            user.SubscriptionEndDate = endDate.ToUniversalTime();
            user.UpdatedAt = DateTime.UtcNow;
            await userDal.Update(user);
            await auditService.RecordAsync(AuditAction.AdminSubscriptionUpdated, adminId, user.Id, null, true);
            return (true, $"Abonelik bitiş tarihi güncellendi: {user.SubscriptionEndDate:yyyy-MM-dd} UTC (gate kapalı).");
        }

        private async Task<(bool, string)> ToolSuspendStoreAsync(Guid adminId, JsonElement input)
        {
            var store = await ResolveStoreAsync(input, "store_id");
            if (store == null) return (false, "Dükkan bulunamadı (GUID veya salon numarası girin).");

            var suspend = GetBool(input, "suspend") ?? true;
            var reason = GetString(input, "reason");
            var result = await barberStoreService.AdminSetSuspendedAsync(adminId, store.Id, suspend, reason);
            return (result.Success, result.Message ?? (suspend ? "Dükkan askıya alındı" : "Askı kaldırıldı"));
        }

        private async Task<(bool, string)> ToolSuspendFreeBarberAsync(Guid adminId, JsonElement input)
        {
            var fb = await ResolveFreeBarberAsync(input, "free_barber_id");
            if (fb == null) return (false, "Serbest berber bulunamadı (GUID veya 6 haneli numara girin).");

            var suspend = GetBool(input, "suspend") ?? true;
            var reason = GetString(input, "reason");
            var result = await freeBarberService.AdminSetSuspendedAsync(adminId, fb.Id, suspend, reason);
            return (result.Success, result.Message ?? (suspend ? "Serbest berber askıya alındı" : "Askı kaldırıldı"));
        }

        private async Task<(bool, string)> ToolResolveComplaintAsync(Guid adminId, JsonElement input)
        {
            if (!TryParseGuid(input, "complaint_id", out var complaintId))
                return (false, "Geçersiz complaint_id");

            var result = await complaintService.ResolveComplaintAsync(adminId, complaintId);
            return (result.Success, result.Message ?? "Şikayet çözümlendi");
        }

        private async Task<(bool, string)> ToolMarkRequestAsync(Guid adminId, JsonElement input)
        {
            if (!TryParseGuid(input, "request_id", out var requestId))
                return (false, "Geçersiz request_id");

            var isProcessed = GetBool(input, "is_processed") ?? true;
            var result = await requestService.MarkProcessedAsync(adminId, requestId, isProcessed);
            return (result.Success, result.Message ?? (isProcessed ? "Talep işlendi" : "Talep işlenmedi"));
        }

        private async Task<(bool, string)> ToolCancelAppointmentAsync(Guid adminId, JsonElement input)
        {
            if (!TryParseGuid(input, "appointment_id", out var appointmentId))
                return (false, "Geçersiz appointment_id");

            var reason = GetString(input, "reason");
            var result = await appointmentService.AdminCancelAsync(adminId, appointmentId, reason);
            return (result.Success, result.Message ?? Messages.AppointmentAdminCancelledSuccess);
        }

        private async Task<(bool, string)> ToolDeleteRatingAsync(Guid adminId, JsonElement input)
        {
            if (!TryParseGuid(input, "rating_id", out var ratingId))
                return (false, "Geçersiz rating_id");

            var result = await ratingService.AdminDeleteRatingAsync(adminId, ratingId);
            return (result.Success, result.Message ?? "Puan silindi");
        }

        private static bool TryParseGuid(JsonElement input, string prop, out Guid id)
        {
            id = Guid.Empty;
            var s = GetString(input, prop);
            return !string.IsNullOrWhiteSpace(s) && Guid.TryParse(s, out id);
        }

        /// <summary>"217-017", "217 bin 017" gibi yazımları "217017" rakam dizisine indirger.</summary>
        private static string NormalizeDigits(string? s) =>
            string.IsNullOrEmpty(s) ? "" : new string(s.Where(char.IsDigit).ToArray());

        /// <summary>Kullanıcıyı GUID veya 6 haneli müşteri/berber numarasıyla çözer.</summary>
        private async Task<Entities.Concrete.Entities.User?> ResolveUserAsync(JsonElement input, string prop)
        {
            var raw = GetString(input, prop);
            if (string.IsNullOrWhiteSpace(raw)) return null;
            if (Guid.TryParse(raw, out var id))
                return await userDal.Get(u => u.Id == id);
            var digits = NormalizeDigits(raw);
            if (digits.Length == 0) return null;
            return await userDal.Get(u => u.CustomerNumber == digits);
        }

        /// <summary>Dükkanı GUID veya salon numarasıyla çözer.</summary>
        private async Task<Entities.Concrete.Entities.BarberStore?> ResolveStoreAsync(JsonElement input, string prop)
        {
            var raw = GetString(input, prop);
            if (string.IsNullOrWhiteSpace(raw)) return null;
            if (Guid.TryParse(raw, out var id))
                return await barberStoreDal.Get(s => s.Id == id);
            var digits = NormalizeDigits(raw);
            if (digits.Length == 0) return null;
            return await barberStoreDal.Get(s => s.StoreNo == digits);
        }

        /// <summary>Serbest berber panelini GUID veya sahibinin 6 haneli numarasıyla çözer.</summary>
        private async Task<Entities.Concrete.Entities.FreeBarber?> ResolveFreeBarberAsync(JsonElement input, string prop)
        {
            var raw = GetString(input, prop);
            if (string.IsNullOrWhiteSpace(raw)) return null;
            if (Guid.TryParse(raw, out var id))
            {
                var byId = await freeBarberDal.Get(f => f.Id == id);
                if (byId != null) return byId;
                // GUID kullanıcı id'si de olabilir
                return await freeBarberDal.Get(f => f.FreeBarberUserId == id);
            }
            var digits = NormalizeDigits(raw);
            if (digits.Length == 0) return null;
            var user = await userDal.Get(u => u.CustomerNumber == digits);
            if (user == null) return null;
            return await freeBarberDal.Get(f => f.FreeBarberUserId == user.Id);
        }

        private static string? GetString(JsonElement input, string prop)
        {
            if (input.ValueKind != JsonValueKind.Object || !input.TryGetProperty(prop, out var el)) return null;
            return el.ValueKind switch
            {
                JsonValueKind.String => el.GetString(),
                JsonValueKind.Number => el.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            };
        }

        private static bool? GetBool(JsonElement input, string prop)
        {
            if (input.ValueKind != JsonValueKind.Object || !input.TryGetProperty(prop, out var el)) return null;
            return el.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String when bool.TryParse(el.GetString(), out var b) => b,
                _ => null
            };
        }

        private static int? GetInt(JsonElement input, string prop)
        {
            if (input.ValueKind != JsonValueKind.Object || !input.TryGetProperty(prop, out var el)) return null;
            if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n)) return n;
            if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var parsed)) return parsed;
            return null;
        }

        private sealed class LlmTurnResponse
        {
            public string? Text { get; init; }
            public List<LlmToolCall> ToolCalls { get; init; } = new();
        }

        private sealed class LlmToolCall
        {
            public string Id { get; init; } = null!;
            public string Name { get; init; } = null!;
            public string ArgumentsJson { get; init; } = null!;
        }

        private sealed class LlmRateLimitException : Exception;
    }
}
