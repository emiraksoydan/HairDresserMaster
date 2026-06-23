using Entities.Abstract;

using Entities.Concrete.Enums;



namespace Entities.Concrete.Entities

{

    /// <summary>

    /// Tamamlanan randevu için katılımcının sosyal paylaşım kaydı (kullanıcı + randevu → tek paylaşım).

    /// </summary>

    public class AppointmentSocialShare : IEntity

    {

        public Guid Id { get; set; }

        public Guid AppointmentId { get; set; }

        public Appointment Appointment { get; set; } = null!;

        public Guid UserId { get; set; }

        public AppointmentSocialShareContentType ContentType { get; set; }

        public Guid ContentId { get; set; }

        public DateTime CreatedAt { get; set; }

    }

}

