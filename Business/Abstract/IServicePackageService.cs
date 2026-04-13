using Core.Utilities.Results;
using Entities.Concrete.Dto;

namespace Business.Abstract
{
    public interface IServicePackageService
    {
        /// <summary>Yeni paket ekler. Limit ve duplicate kontrolü yapar.</summary>
        Task<IResult> AddAsync(ServicePackageCreateDto dto, Guid currentUserId);

        /// <summary>Var olan paketi günceller. Aktif randevu ve duplicate kontrolü yapar.</summary>
        Task<IResult> UpdateAsync(ServicePackageUpdateDto dto, Guid currentUserId);

        /// <summary>Paketi siler. Aktif/bekleyen randevu varsa izin vermez.</summary>
        Task<IResult> DeleteAsync(Guid packageId, Guid currentUserId);

        /// <summary>Sahibine ait tüm paketleri getirir.</summary>
        Task<IDataResult<List<ServicePackageGetDto>>> GetAllByOwnerAsync(Guid ownerId, Guid currentUserId);

        /// <summary>Randevuya ait paket snapshot'larını getirir.</summary>
        Task<IDataResult<List<AppointmentServicePackageDto>>> GetPackagesByAppointmentAsync(Guid appointmentId);
    }
}
