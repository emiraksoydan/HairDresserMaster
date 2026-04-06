namespace Entities.Concrete.Dto
{
    /// <summary>POST Appointment/{id}/cancel — iptal nedeni isteğe bağlıdır.</summary>
    public class CancelAppointmentRequestDto
    {
        public const int CancellationReasonMaxLength = 500;

        public string? CancellationReason { get; set; }
    }
}
