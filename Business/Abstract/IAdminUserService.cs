using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Utilities.Results;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Http;

namespace Business.Abstract
{
    /// <summary>
    /// Admin (yönetim paneli kullanıcısı) CRUD'u. Sadece Admin role'üne sahip
    /// JWT taşıyan istekler tarafından çağrılabilir. Endpoint guard'ı AdminController'da.
    /// </summary>
    public interface IAdminUserService
    {
        Task<IDataResult<List<AdminUserListItemDto>>> GetAllAsync();
        Task<IDataResult<AdminUserListItemDto>> CreateAsync(AdminUserCreateDto dto, Guid actingAdminId);
        Task<IResult> SetActiveAsync(Guid targetAdminId, bool isActive, Guid actingAdminId);
        Task<IResult> DeleteAsync(Guid targetAdminId, Guid actingAdminId);
        Task<IDataResult<AdminUserListItemDto>> UpdateProfileAsync(Guid actingAdminId, AdminUserUpdateProfileDto dto);
        Task<IResult> ChangePasswordAsync(Guid actingAdminId, AdminUserChangePasswordDto dto);
        Task<IDataResult<AdminUserListItemDto>> GetMeAsync(Guid actingAdminId);
        Task<IDataResult<AdminUserListItemDto>> UploadAvatarAsync(Guid actingAdminId, IFormFile file);
        Task<IDataResult<AdminUserListItemDto>> RemoveAvatarAsync(Guid actingAdminId);
    }
}
