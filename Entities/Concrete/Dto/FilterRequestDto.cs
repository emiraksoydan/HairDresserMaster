using Entities.Abstract;
using Entities.Attributes;
using Entities.Concrete.Enums;

namespace Entities.Concrete.Dto
{
    /// <summary>
    /// Filtreleme ve arama için request DTO
    /// </summary>
    public class FilterRequestDto : IDto
    {
        // Konum bilgileri (nearby için)
        [LogIgnore]
        public double? Latitude { get; set; }
        [LogIgnore]
        public double? Longitude { get; set; }
        public double DistanceKm { get; set; } = FilterConstants.DefaultDistanceKm;

        // Arama
        public string? SearchQuery { get; set; }

        // Ana kategori filtresi (BarberType)
        public BarberType? MainCategory { get; set; } // null = Hepsi

        // Hizmet filtresi (CategoryId listesi)
        public List<Guid>? ServiceIds { get; set; }

        // Fiyat filtresi
        public string? PriceSort { get; set; } // "none", "asc", "desc"
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }

        // Pricing Type (Store için)
        public string? PricingType { get; set; } // "all", "rent", "percent"

        /// <summary>
        /// Mağaza: Ready = açık, NotReady = kapalı. Serbest berber: Ready = müsait, NotReady = meşgul.
        /// </summary>
        public AvailabilityFilter? Availability { get; set; }

        // Puanlama
        public int? MinRating { get; set; } // 0-5

        // Favoriler
        public bool? FavoritesOnly { get; set; }
        
        // Kullanıcı ID (favoriler ve diğer kullanıcıya özel filtreler için)
        public Guid? CurrentUserId { get; set; }
    }
}

