using System.Collections.Generic;
using System.Threading.Tasks;
using Business.Abstract;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;

namespace Business.Concrete
{
    public class AdminSearchManager(IAdminSearchDal adminSearchDal) : IAdminSearchService
    {
        public async Task<IDataResult<List<AdminSearchResultDto>>> SearchAsync(string query, int limit = 20, string? kind = null)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new SuccessDataResult<List<AdminSearchResultDto>>(new List<AdminSearchResultDto>());

            var maxLimit = string.IsNullOrWhiteSpace(kind) ? 50 : 500;
            var clampedLimit = limit < 1 ? 20 : (limit > maxLimit ? maxLimit : limit);
            var results = await adminSearchDal.SearchAsync(query.Trim(), clampedLimit, kind);
            return new SuccessDataResult<List<AdminSearchResultDto>>(results);
        }
    }
}
