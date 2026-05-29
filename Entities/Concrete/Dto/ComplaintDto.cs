using System;
using Entities.Concrete.Enums;

namespace Entities.Concrete.Dto
{
    /// <summary>
    /// Şikayet oluşturma DTO
    /// </summary>
    public class CreateComplaintDto
    {
        public Guid ComplaintToUserId { get; set; }
        public Guid? AppointmentId { get; set; }
        public string ComplaintReason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Şikayet görüntüleme DTO
    /// </summary>
    public class ComplaintGetDto
    {
        public Guid Id { get; set; }
        public Guid ComplaintFromUserId { get; set; }
        public Guid ComplaintToUserId { get; set; }
        public Guid? AppointmentId { get; set; }
        public string ComplaintReason { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsResolved { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public Guid? ResolvedByAdminId { get; set; }

        // Şikayet edilen kullanıcı bilgisi
        public string? TargetUserName { get; set; }
        public string? TargetUserImage { get; set; }
        public UserType? TargetUserType { get; set; }
        public string? TargetCustomerNumber { get; set; }

        // Şikayet eden kullanıcı bilgisi (admin listesi)
        public string? ComplaintFromUserName { get; set; }
        public string? ComplaintFromUserImage { get; set; }
        public UserType? ComplaintFromUserType { get; set; }
        public string? ComplaintFromCustomerNumber { get; set; }
    }
}
