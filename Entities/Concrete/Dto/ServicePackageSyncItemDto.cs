namespace Entities.Concrete.Dto
{
    /// <summary>
    /// Dükkan / serbest berber kaydı ile birlikte paket senkronu (ekle-güncelle-sil).
    /// </summary>
    public class ServicePackageSyncItemDto
    {
        public Guid? Id { get; set; }
        public string PackageName { get; set; } = string.Empty;
        public decimal TotalPrice { get; set; }
        public List<Guid> ServiceOfferingIds { get; set; } = new();
        /// <summary>Oluşturma veya isimle eşleme: offering ServiceName listesi.</summary>
        public List<string>? ServiceNames { get; set; }
    }
}
