using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities.Abstract;
using Entities.Attributes;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;

namespace Entities.Concrete.Dto
{
    public class FreeBarberGetDto : IDto
    {
        public Guid Id { get; set; }
        public Guid FreeBarberUserId { get; set; }
        public string FullName { get; set; }
        public BarberType Type { get; set; }
        public double Rating { get; set; }
        public int FavoriteCount { get; set; }
        public bool IsFavorited { get; set; }
        public bool IsAvailable { get; set; }
        public double DistanceKm { get; set; }
        public int ReviewCount { get; set; }
        [LogIgnore]
        public double Latitude { get; set; }
        [LogIgnore]
        public double Longitude { get; set; }
        public List<ImageGetDto> ImageList { get; set; }
        public List<ServiceOfferingGetDto> Offerings { get; set; }
        public bool IsOwnPanel { get; set; } // Kullanıcının kendi paneli mi (filtrelerden etkilenmez)
        /// <summary>Güzellik salonu sertifikası varsa dolu; kartta "Güzellik Uzmanı" chip gösterilir.</summary>
        public Guid? BeautySalonCertificateImageId { get; set; }
        public string? CustomerNumber { get; set; } // Serbest berberin müşteri numarası (User.CustomerNumber)
    }
}

