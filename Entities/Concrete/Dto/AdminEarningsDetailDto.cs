namespace Entities.Concrete.Dto
{
    public class AdminEarningsDetailDto
    {
        public EarningsDto Summary { get; set; } = new();
        public List<AdminEarningAppointmentRowDto> Appointments { get; set; } = new();
    }

    public class AdminEarningAppointmentRowDto
    {
        public Guid AppointmentId { get; set; }
        public DateTime CompletedAt { get; set; }
        public string? CustomerDisplayName { get; set; }
        public string? CounterpartyDisplayName { get; set; }
        public decimal ServicesTotal { get; set; }
        public decimal EarningAmount { get; set; }
        public string? ServiceSummary { get; set; }
    }
}
