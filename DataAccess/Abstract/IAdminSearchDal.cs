using System.Collections.Generic;
using System.Threading.Tasks;
using Entities.Concrete.Dto;

namespace DataAccess.Abstract
{
    public interface IAdminSearchDal
    {
        Task<List<AdminSearchResultDto>> SearchAsync(string query, int limit, string? kind = null);
    }
}
