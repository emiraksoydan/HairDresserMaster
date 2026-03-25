using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.DataAccess;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;

namespace DataAccess.Abstract
{
    public interface IManuelBarberDal : IEntityRepository<ManuelBarber>
    {
        Task<List<ManuelBarberRatingDto>> GetManuelBarberRatingsAsync(List<Guid> barberIds);
        Task<List<ManuelBarberDto>> GetBarberDtosByStoreIdAsync(Guid storeId);
        Task<List<ManuelBarberAdminGetDto>> GetAllForAdminAsync();
    }
}
