using System;
using System.Globalization;
using System.Text.Json;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;

namespace Business.Helpers
{
    /// <summary>
    /// "Tümünü okundu" sırasında kabul/red bekleyen bildirimleri hariç tutar.
    /// Mobil <c>NotificationItemOptimized</c> içindeki <c>canShowButtons</c> ile aynı mantık — değişince iki tarafı güncelleyin.
    /// </summary>
    public static class NotificationMarkAllReadExclusion
    {
        public static bool ShouldKeepUnreadForPendingActions(
            Notification notification,
            Appointment? appointment,
            UserType userType,
            DateTime utcNow)
        {
            if (notification.Type != NotificationType.AppointmentCreated &&
                notification.Type != NotificationType.StoreApprovedSelection)
                return false;

            if (!TryReadPayload(notification.PayloadJson, out var p))
                p = default;

            var appointmentStatus = ResolveAppointmentStatus(notification.Type, p);
            var storeDecision = PickDecision(p.StoreDecision, appointment?.StoreDecision);
            var freeBarberDecision = PickDecision(p.FreeBarberDecision, appointment?.FreeBarberDecision);
            var customerDecision = PickDecision(p.CustomerDecision, appointment?.CustomerDecision);

            var isExpiredCheck = ComputeIsExpired(
                p,
                appointmentStatus,
                notification.CreatedAt,
                userType,
                storeDecision,
                utcNow);

            var finalStatus = isExpiredCheck && appointmentStatus == AppointmentStatus.Pending
                ? AppointmentStatus.Unanswered
                : appointmentStatus;

            if (finalStatus != AppointmentStatus.Pending)
                return false;

            var myDecision = GetMyDecision(userType, p.RecipientRole, storeDecision, freeBarberDecision, customerDecision);
            var hasMyDecision = myDecision.HasValue && myDecision.Value != DecisionStatus.Pending;

            if (HasBlockingStatusRibbon(finalStatus, notification.Type, hasMyDecision, myDecision))
                return false;

            if (hasMyDecision)
                return false;

            if (storeDecision == DecisionStatus.Rejected ||
                freeBarberDecision == DecisionStatus.Rejected ||
                customerDecision == DecisionStatus.Rejected)
                return false;

            if (storeDecision == DecisionStatus.NoAnswer ||
                freeBarberDecision == DecisionStatus.NoAnswer ||
                customerDecision == DecisionStatus.NoAnswer)
                return false;

            return userType switch
            {
                UserType.BarberStore =>
                    storeDecision == null || storeDecision == DecisionStatus.Pending,

                UserType.FreeBarber => FreeBarberCanAct(p, storeDecision, freeBarberDecision),

                UserType.Customer => CustomerCanAct(p, storeDecision, customerDecision),

                _ => false
            };
        }

        private static bool FreeBarberCanAct(PayloadSnap p, DecisionStatus? storeDecision, DecisionStatus? freeBarberDecision)
        {
            if (p.StoreSelectionType == StoreSelectionType.StoreSelection)
            {
                if (p.HasStoreObject && storeDecision == DecisionStatus.Pending)
                    return false;

                if (!p.HasStoreObject)
                    return freeBarberDecision == null || freeBarberDecision == DecisionStatus.Pending;

                if (storeDecision == DecisionStatus.Approved)
                    return false;
            }

            return freeBarberDecision == null || freeBarberDecision == DecisionStatus.Pending;
        }

        private static bool CustomerCanAct(PayloadSnap p, DecisionStatus? storeDecision, DecisionStatus? customerDecision)
        {
            if (p.StoreSelectionType == StoreSelectionType.StoreSelection)
            {
                return p.HasStoreObject &&
                       storeDecision == DecisionStatus.Approved &&
                       (customerDecision == null || customerDecision == DecisionStatus.Pending);
            }

            return customerDecision == null || customerDecision == DecisionStatus.Pending;
        }

        private readonly struct PayloadSnap
        {
            public int? Status { get; init; }
            public int? StoreDecision { get; init; }
            public int? FreeBarberDecision { get; init; }
            public int? CustomerDecision { get; init; }
            public string? RecipientRole { get; init; }
            public StoreSelectionType? StoreSelectionType { get; init; }
            public bool HasStoreObject { get; init; }
            public string? PendingExpiresAt { get; init; }
        }

        private static bool TryReadPayload(string json, out PayloadSnap p)
        {
            p = default;
            if (string.IsNullOrWhiteSpace(json) || json.Trim() == "{}")
                return false;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                p = new PayloadSnap
                {
                    Status = ReadInt(root, "status"),
                    StoreDecision = ReadInt(root, "storeDecision"),
                    FreeBarberDecision = ReadInt(root, "freeBarberDecision"),
                    CustomerDecision = ReadInt(root, "customerDecision"),
                    RecipientRole = ReadString(root, "recipientRole"),
                    StoreSelectionType = ReadEnum<StoreSelectionType>(root, "storeSelectionType"),
                    HasStoreObject = root.TryGetProperty("store", out var st) && st.ValueKind == JsonValueKind.Object,
                    PendingExpiresAt = ReadString(root, "pendingExpiresAt")
                };
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static int? ReadInt(JsonElement root, string name)
        {
            if (!root.TryGetProperty(name, out var el))
                return null;
            return el.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.Number when el.TryGetInt32(out var i) => i,
                JsonValueKind.String when int.TryParse(el.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var j) => j,
                _ => null
            };
        }

        private static string? ReadString(JsonElement root, string name)
        {
            if (!root.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.String)
                return null;
            return el.GetString();
        }

        private static TEnum? ReadEnum<TEnum>(JsonElement root, string name) where TEnum : struct, Enum
        {
            var i = ReadInt(root, name);
            if (!i.HasValue || !Enum.IsDefined(typeof(TEnum), i.Value))
                return null;
            return (TEnum)(object)i.Value;
        }

        private static AppointmentStatus ResolveAppointmentStatus(NotificationType type, PayloadSnap p)
        {
            if (p.Status.HasValue && Enum.IsDefined(typeof(AppointmentStatus), p.Status.Value))
                return (AppointmentStatus)p.Status.Value;
            if (type == NotificationType.AppointmentUnanswered)
                return AppointmentStatus.Unanswered;
            // Payload yoksa Pending — mobil `NotificationItemOptimized` / TS exclusion ile aynı (canlı Appointment.Status kullanılmaz).
            return AppointmentStatus.Pending;
        }

        private static DecisionStatus? PickDecision(int? fromPayload, DecisionStatus? fromAppointment)
        {
            if (fromPayload.HasValue && Enum.IsDefined(typeof(DecisionStatus), fromPayload.Value))
                return (DecisionStatus)fromPayload.Value;
            return fromAppointment;
        }

        private static DecisionStatus? GetMyDecision(
            UserType userType,
            string? recipientRole,
            DecisionStatus? store,
            DecisionStatus? fb,
            DecisionStatus? cust)
        {
            var role = recipientRole?.Trim().ToLowerInvariant() switch
            {
                "store" => "store",
                "freebarber" => "freebarber",
                "customer" => "customer",
                _ => userType switch
                {
                    UserType.BarberStore => "store",
                    UserType.FreeBarber => "freebarber",
                    UserType.Customer => "customer",
                    _ => null
                }
            };

            return role switch
            {
                "store" => store,
                "freebarber" => fb,
                "customer" => cust,
                _ => null
            };
        }

        /// <summary>Frontend'de statusKind != null iken kabul/red butonları gösterilmez.</summary>
        private static bool HasBlockingStatusRibbon(
            AppointmentStatus finalStatus,
            NotificationType itemType,
            bool hasMyDecision,
            DecisionStatus? myDecision)
        {
            if (finalStatus == AppointmentStatus.Approved) return true;
            if (finalStatus == AppointmentStatus.Rejected) return true;
            if (finalStatus == AppointmentStatus.Cancelled) return true;
            if (finalStatus == AppointmentStatus.Completed) return true;
            if (finalStatus == AppointmentStatus.Unanswered) return true;

            if (itemType == NotificationType.AppointmentApproved) return true;
            if (itemType == NotificationType.AppointmentRejected) return true;
            if (itemType == NotificationType.AppointmentCancelled) return true;
            if (itemType == NotificationType.AppointmentCompleted) return true;
            if (itemType == NotificationType.AppointmentUnanswered) return true;

            if (finalStatus == AppointmentStatus.Pending && hasMyDecision)
            {
                if (myDecision == DecisionStatus.Approved) return true;
                if (myDecision == DecisionStatus.Rejected) return true;
                if (myDecision == DecisionStatus.NoAnswer) return true;
            }

            return false;
        }

        private static bool ComputeIsExpired(
            PayloadSnap p,
            AppointmentStatus appointmentStatus,
            DateTime notificationCreatedAtUtc,
            UserType userType,
            DecisionStatus? storeDecision,
            DateTime utcNow)
        {
            if (!string.IsNullOrWhiteSpace(p.PendingExpiresAt))
            {
                if (DateTime.TryParse(
                        p.PendingExpiresAt,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                        out var exp))
                    return utcNow > exp;
            }

            if (appointmentStatus != AppointmentStatus.Pending)
                return false;

            var created = notificationCreatedAtUtc.Kind == DateTimeKind.Utc
                ? notificationCreatedAtUtc
                : notificationCreatedAtUtc.ToUniversalTime();

            var isStoreSelectionFlow = p.StoreSelectionType == StoreSelectionType.StoreSelection;
            var isCustomerWaitingForStore =
                isStoreSelectionFlow &&
                userType == UserType.Customer &&
                p.HasStoreObject &&
                storeDecision == DecisionStatus.Approved;

            var timeoutMinutes = isCustomerWaitingForStore ? 30 : 5;
            return utcNow > created.AddMinutes(timeoutMinutes);
        }
    }
}
