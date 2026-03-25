using Core.Utilities.Results;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Business.Abstract
{
    public interface IServiceOfferingService
    {
        Task<IResult> AddRangeAsync(List<ServiceOffering> list);
        Task<IResult> UpdateRange(List<ServiceOfferingUpdateDto> serviceOfferingUpdateDto, Guid currentUserId);
        Task<IDataResult<List<ServiceOfferingAdminGetDto>>> GetAllForAdminAsync();
    }
}
