using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Utilities.Results;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;

namespace Business.Abstract
{
    public interface IWorkingHourService
    {
        Task<IResult> AddRangeAsync(List<WorkingHour> dto);

        Task<IResult> UpdateRangeAsync(List<WorkingHourUpdateDto> dto);

        Task<IDataResult<List<WorkingHourDto>>> GetByTargetAsync(Guid targetId);
    }
}
