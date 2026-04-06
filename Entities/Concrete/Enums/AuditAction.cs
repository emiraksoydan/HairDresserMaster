namespace Entities.Concrete.Enums
{
    /// <summary>Denetim günlüğü olay türü (sabit kod; mesaj/şikayet metni tutulmaz).</summary>
    public enum AuditAction
    {
        AccountClosed = 1,
        ChatMessageSentAppointmentThread = 2,
        ChatMessageSentFavoriteThread = 3,
        ChatMediaMessageSent = 4,
        ChatMessageHiddenForUser = 5,
        ChatThreadHiddenForUser = 6,

        AuthOtpVerificationFailed = 7,
        AuthLoginSuccess = 8,
        AuthRegisterSuccess = 9,
        AuthRefreshSuccess = 10,
        AuthLogout = 11,

        AppointmentCreated = 12,
        AppointmentStoreLinkedToExisting = 13,
        AppointmentApprovedByStore = 14,
        AppointmentRejectedByStore = 15,
        AppointmentApprovedByFreeBarber = 16,
        AppointmentRejectedByFreeBarber = 17,
        AppointmentApprovedByCustomer = 18,
        AppointmentRejectedByCustomer = 19,
        AppointmentCancelled = 20,
        AppointmentCompleted = 21,
        AppointmentHiddenByUser = 22,
        AppointmentHiddenByUserBulk = 23,

        BarberStoreDeleted = 24,
        FreeBarberPanelDeleted = 25,
        ComplaintCreated = 26,
        RatingDeleted = 27,
        ManuelBarberDeleted = 28
    }
}
