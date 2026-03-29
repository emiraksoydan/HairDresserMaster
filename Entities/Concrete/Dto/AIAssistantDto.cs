using Entities.Abstract;

namespace Entities.Concrete.Dto
{
    public class AIAssistantRequestDto : IDto
    {
        public string Message { get; set; } = "";
        /// <summary>Dil kodu: tr, en, de, ar</summary>
        public string Language { get; set; } = "tr";
        /// <summary>Kullanıcının konumu — yakın berber/dükkan araması için (opsiyonel)</summary>
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }

    public class AIAssistantResponseDto : IDto
    {
        /// <summary>Kullanıcıya gösterilecek doğal dil yanıtı</summary>
        public string Response { get; set; } = "";
        /// <summary>Tespit edilen niyet: list_appointments, approve_appointment, reject_appointment, cancel_appointment, bulk_decide, get_appointment_details, unknown</summary>
        public string Intent { get; set; } = "unknown";
        /// <summary>Bir aksiyon gerçekleştirildi mi</summary>
        public bool ActionTaken { get; set; }
        /// <summary>Tekil aksiyon için randevu ID'si</summary>
        public Guid? AffectedAppointmentId { get; set; }
        /// <summary>Çoklu (bulk) aksiyon için etkilenen randevu ID'leri</summary>
        public List<Guid> AffectedAppointmentIds { get; set; } = new();
    }
}
