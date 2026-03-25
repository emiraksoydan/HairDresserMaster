using Core.Utilities.Results;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Business.Abstract
{
    public interface IBarberStoreChairService
    {
        Task<IResult> AddAsync(BarberChairCreateDto dto, Guid currentUserId);
        Task<IResult> AddRangeAsync(List<BarberChair> list);

        Task<IResult> UpdateAsync(BarberChairUpdateDto dto, Guid currentUserId);
        Task<IResult> DeleteAsync(Guid id, Guid currentUserId);
        Task<IDataResult<bool>> AttemptBarberControl(Guid id);
        Task<IDataResult<List<BarberChairDto>>> GetAllByStoreAsync(Guid storeId, Guid currentUserId);

        Task<IDataResult<BarberChairDto>> GetById(Guid id, Guid currentUserId);

        /// <summary>Tüm koltuklar (yalnızca Admin rolü).</summary>
        Task<IDataResult<List<BarberChairAdminDto>>> GetAllForAdminAsync();
    }
}
