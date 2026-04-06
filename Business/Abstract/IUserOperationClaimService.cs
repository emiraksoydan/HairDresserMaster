using Core.Utilities.Results;
using Entities.Concrete.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Business.Abstract
{
    public interface IUserOperationClaimService
    {
        Task<IDataResult<List<UserOperationClaim>>> GetClaimByUserId(Guid userId);

        Task<IDataResult<List<UserOperationClaim>>> AddUserOperationsClaim(List<UserOperationClaim> userOperationClaims);

        /// <summary>Kullanıcıya atanmış tüm yetki kayıtlarını kaldırır (OperationClaim tanımları silinmez).</summary>
        Task<IResult> RemoveAllClaimsForUserAsync(Guid userId);
    }
}
