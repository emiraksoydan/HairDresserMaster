using Core.Utilities.Results;
using Entities.Concrete.Dto;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Business.Abstract
{
    public interface IManuelBarberService
    {
        Task<IResult> AddAsync(ManuelBarberCreateDto dto, Guid currentUserId);

        Task<IResult> AddRangeAsync(List<ManuelBarberCreateDto> list, Guid storeId);
        Task<IResult> UpdateAsync(ManuelBarberUpdateDto dto, Guid currentUserId);
        Task<IResult> DeleteAsync(Guid id, Guid currentUserId);
        Task<IDataResult<List<ManuelBarberDto>>> GetAllByStoreAsync(Guid storeId, Guid currentUserId);
        Task<IDataResult<List<ManuelBarberAdminGetDto>>> GetAllForAdminAsync();
    }
}
