using Core.Utilities.Results;
using Entities.Concrete.Dto;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Business.Abstract
{
    public interface IComplaintService
    {
        Task<IDataResult<ComplaintGetDto>> CreateComplaintAsync(Guid userId, CreateComplaintDto dto);
        Task<IDataResult<List<ComplaintGetDto>>> GetMyComplaintsAsync(Guid userId);
        Task<IDataResult<bool>> DeleteComplaintAsync(Guid userId, Guid complaintId);
        Task<IDataResult<List<ComplaintGetDto>>> GetAllComplaintsForAdminAsync();

        /// <summary>Hesap kapanışı: kullanıcıyı içeren şikayetleri KVKK uyumlu şekilde yumuşak siler ve metni anonimleştirir. HelpGuide/kategori/rol tanımlarına dokunulmaz.</summary>
        Task SoftDeleteAllInvolvingUserForAccountClosureAsync(Guid userId);
    }
}
