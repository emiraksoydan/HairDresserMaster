using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Utilities.Results;
using Entities.Concrete.Dto;

namespace Business.Abstract
{
    public interface IAdminSearchService
    {
        Task<IDataResult<List<AdminSearchResultDto>>> SearchAsync(string query, int limit = 20, string? kind = null);
    }
}
