using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Business.Abstract;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Business.Concrete
{
    public class WorkingHourManager(IWorkingHourDal workingHourDal) : IWorkingHourService
    {
        public async Task<IResult> AddRangeAsync(List<WorkingHour> list)
        {
            await workingHourDal.AddRange(list);
            return new SuccessResult("Çalışma saatleri başarıyla oluşturuldu.");
        }

        public async Task<IDataResult<List<WorkingHourDto>>> GetByTargetAsync(Guid targetId)
        {
            var list = await workingHourDal.GetQueryable()
                .AsNoTracking()
                .Where(x => x.OwnerId == targetId)
                .ToListAsync();
            var dto = list.Adapt<List<WorkingHourDto>>();
            return new SuccessDataResult<List<WorkingHourDto>>(dto);
        }

        public async Task<IResult> UpdateRangeAsync(List<WorkingHourUpdateDto> dto)
        {
            var entities = dto.Adapt<List<WorkingHour>>();
            await workingHourDal.UpdateRange(entities);
            return new SuccessResult("Saatler Güncellendi.");
        }
    }
}
