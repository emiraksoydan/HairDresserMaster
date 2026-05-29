using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Business.Abstract;
using Business.Helpers;
using Business.Resources;
using Core.Utilities.Results;
using Core.Utilities.Storage;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Business.Concrete
{
    /// <summary>
    /// Admin (yönetim paneli) kullanıcılarının CRUD'u. Audit kaydı her aksiyonda yazılır.
    /// </summary>
    public class AdminUserManager(
        IAdminUserDal adminUserDal,
        IAuditService auditService,
        IBlobStorageService blobStorageService,
        ILogger<AdminUserManager> logger) : IAdminUserService
    {
        private const string AdminAvatarContainer = "admin-images";

        public async Task<IDataResult<List<AdminUserListItemDto>>> GetAllAsync()
        {
            var admins = await adminUserDal.GetAll();
            var items = admins
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => Map(a))
                .ToList();
            return new SuccessDataResult<List<AdminUserListItemDto>>(items);
        }

        public async Task<IDataResult<AdminUserListItemDto>> CreateAsync(AdminUserCreateDto dto, Guid actingAdminId)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
                return new ErrorDataResult<AdminUserListItemDto>(null!, Messages.AdminAuthCredentialsRequired);
            if (dto.Password.Length < 8)
                return new ErrorDataResult<AdminUserListItemDto>(null!, Messages.AdminAuthResetPasswordTooShort);

            var email = dto.Email.Trim().ToLowerInvariant();

            var existing = await adminUserDal.GetByEmail(email);
            if (existing != null)
            {
                await auditService.RecordAsync(AuditAction.AdminCreated, actingAdminId, null, null, false, "EmailAlreadyExists");
                return new ErrorDataResult<AdminUserListItemDto>(null!, Messages.AdminMgmtEmailAlreadyExists);
            }

            var entity = new AdminUser
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                FullName = string.IsNullOrWhiteSpace(dto.FullName) ? "Admin" : dto.FullName.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await adminUserDal.Add(entity);

            await auditService.RecordAsync(AuditAction.AdminCreated, actingAdminId, entity.Id, null, true);
            logger.LogInformation("[AdminMgmt] Yeni admin oluşturuldu | ActorId: {ActorId} | NewAdminId: {NewId} | Email: {Email}",
                actingAdminId, entity.Id, entity.Email);

            return new SuccessDataResult<AdminUserListItemDto>(Map(entity), Messages.AdminMgmtCreated);
        }

        public async Task<IResult> SetActiveAsync(Guid targetAdminId, bool isActive, Guid actingAdminId)
        {
            if (targetAdminId == actingAdminId)
                return new ErrorResult(Messages.AdminMgmtCannotModifySelf);

            var admin = await adminUserDal.Get(a => a.Id == targetAdminId);
            if (admin == null)
                return new ErrorResult(Messages.AdminAuthUserNotFound);

            if (admin.IsActive == isActive)
                return new SuccessResult(Messages.OperationSuccess);

            admin.IsActive = isActive;
            admin.UpdatedAt = DateTime.UtcNow;
            // Pasifleştirme: var olan refresh token'ı geçersiz kıl.
            if (!isActive)
            {
                admin.RefreshTokenHash = null;
                admin.RefreshTokenExpiresAt = null;
            }
            await adminUserDal.Update(admin);

            var action = isActive ? AuditAction.AdminActivated : AuditAction.AdminDeactivated;
            await auditService.RecordAsync(action, actingAdminId, targetAdminId, null, true);
            logger.LogInformation("[AdminMgmt] Admin {Action} | ActorId: {ActorId} | TargetId: {TargetId}",
                isActive ? "aktive edildi" : "pasifleştirildi", actingAdminId, targetAdminId);

            return new SuccessResult(isActive ? Messages.AdminMgmtActivated : Messages.AdminMgmtDeactivated);
        }

        public async Task<IResult> DeleteAsync(Guid targetAdminId, Guid actingAdminId)
        {
            if (targetAdminId == actingAdminId)
                return new ErrorResult(Messages.AdminMgmtCannotDeleteSelf);

            var admin = await adminUserDal.Get(a => a.Id == targetAdminId);
            if (admin == null)
                return new ErrorResult(Messages.AdminAuthUserNotFound);

            // En az bir admin kalmalı.
            var all = await adminUserDal.GetAll();
            if (all.Count <= 1)
                return new ErrorResult(Messages.AdminMgmtCannotDeleteLast);

            await adminUserDal.Remove(admin);
            await auditService.RecordAsync(AuditAction.AdminDeleted, actingAdminId, targetAdminId, null, true);
            logger.LogInformation("[AdminMgmt] Admin silindi | ActorId: {ActorId} | TargetId: {TargetId} | Email: {Email}",
                actingAdminId, targetAdminId, admin.Email);

            return new SuccessResult(Messages.AdminMgmtDeleted);
        }

        public async Task<IDataResult<AdminUserListItemDto>> GetMeAsync(Guid actingAdminId)
        {
            var admin = await adminUserDal.Get(a => a.Id == actingAdminId);
            if (admin == null)
                return new ErrorDataResult<AdminUserListItemDto>(null!, Messages.AdminAuthUserNotFound);
            return new SuccessDataResult<AdminUserListItemDto>(Map(admin));
        }

        public async Task<IDataResult<AdminUserListItemDto>> UpdateProfileAsync(Guid actingAdminId, AdminUserUpdateProfileDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.FullName))
                return new ErrorDataResult<AdminUserListItemDto>(null!, Messages.AdminAuthCredentialsRequired);

            var admin = await adminUserDal.Get(a => a.Id == actingAdminId);
            if (admin == null)
                return new ErrorDataResult<AdminUserListItemDto>(null!, Messages.AdminAuthUserNotFound);

            var newEmail = dto.Email.Trim().ToLowerInvariant();
            if (!string.Equals(newEmail, admin.Email, StringComparison.OrdinalIgnoreCase))
            {
                var existing = await adminUserDal.GetByEmail(newEmail);
                if (existing != null && existing.Id != admin.Id)
                    return new ErrorDataResult<AdminUserListItemDto>(null!, Messages.AdminMgmtEmailAlreadyExists);
                admin.Email = newEmail;
            }

            admin.FullName = dto.FullName.Trim();
            admin.UpdatedAt = DateTime.UtcNow;
            await adminUserDal.Update(admin);

            await auditService.RecordAsync(AuditAction.AdminProfileUpdated, actingAdminId, actingAdminId, null, true);
            logger.LogInformation("[AdminMgmt] Admin profili güncellendi | ActorId: {ActorId}", actingAdminId);

            return new SuccessDataResult<AdminUserListItemDto>(Map(admin), Messages.AdminMgmtProfileUpdated);
        }

        public async Task<IResult> ChangePasswordAsync(Guid actingAdminId, AdminUserChangePasswordDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.CurrentPassword) || string.IsNullOrWhiteSpace(dto.NewPassword))
                return new ErrorResult(Messages.AdminAuthCredentialsRequired);
            if (dto.NewPassword.Length < 8)
                return new ErrorResult(Messages.AdminAuthResetPasswordTooShort);

            var admin = await adminUserDal.Get(a => a.Id == actingAdminId);
            if (admin == null)
                return new ErrorResult(Messages.AdminAuthUserNotFound);

            if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, admin.PasswordHash))
            {
                await auditService.RecordAsync(AuditAction.AdminPasswordChanged, actingAdminId, actingAdminId, null, false, "CurrentPasswordWrong");
                return new ErrorResult(Messages.AdminMgmtCurrentPasswordWrong);
            }

            admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            admin.UpdatedAt = DateTime.UtcNow;
            await adminUserDal.Update(admin);

            await auditService.RecordAsync(AuditAction.AdminPasswordChanged, actingAdminId, actingAdminId, null, true);
            logger.LogInformation("[AdminMgmt] Admin şifresi değiştirildi | ActorId: {ActorId}", actingAdminId);
            return new SuccessResult(Messages.AdminMgmtPasswordChanged);
        }

        public async Task<IDataResult<AdminUserListItemDto>> UploadAvatarAsync(Guid actingAdminId, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return new ErrorDataResult<AdminUserListItemDto>(null!, Messages.AdminMgmtAvatarFileRequired);

            var validation = UploadFileValidator.ValidateProfileOrOwnerImage(file);
            if (!validation.Success)
                return new ErrorDataResult<AdminUserListItemDto>(null!, validation.Message);

            var admin = await adminUserDal.Get(a => a.Id == actingAdminId);
            if (admin == null)
                return new ErrorDataResult<AdminUserListItemDto>(null!, Messages.AdminAuthUserNotFound);

            // Eski avatar varsa blob'tan temizle.
            if (!string.IsNullOrWhiteSpace(admin.ProfileImageUrl))
            {
                try
                {
                    // URL'de query-string varsa kaldır (örn. ?t=cache-buster).
                    var oldUrl = admin.ProfileImageUrl;
                    var qIdx = oldUrl.IndexOf('?');
                    var cleanOld = qIdx >= 0 ? oldUrl.Substring(0, qIdx) : oldUrl;
                    await blobStorageService.DeleteAsync(cleanOld);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[AdminMgmt] Eski avatar silinemedi | ActorId: {ActorId}", actingAdminId);
                }
            }

            var sanitized = UploadFileValidator.GetSanitizedFileName(validation, fallback: file.FileName ?? "avatar");
            var ext = System.IO.Path.GetExtension(sanitized);
            var safeBlobName = $"{Guid.NewGuid()}{ext}";
            var imageUrl = await blobStorageService.UploadAsync(file, AdminAvatarContainer, safeBlobName);
            var urlWithTimestamp = $"{imageUrl}?t={DateTime.UtcNow.Ticks}";

            admin.ProfileImageUrl = urlWithTimestamp;
            admin.UpdatedAt = DateTime.UtcNow;
            await adminUserDal.Update(admin);

            await auditService.RecordAsync(AuditAction.AdminProfileUpdated, actingAdminId, actingAdminId, null, true, "AvatarUploaded");
            return new SuccessDataResult<AdminUserListItemDto>(Map(admin), Messages.AdminMgmtProfileUpdated);
        }

        public async Task<IDataResult<AdminUserListItemDto>> RemoveAvatarAsync(Guid actingAdminId)
        {
            var admin = await adminUserDal.Get(a => a.Id == actingAdminId);
            if (admin == null)
                return new ErrorDataResult<AdminUserListItemDto>(null!, Messages.AdminAuthUserNotFound);

            if (!string.IsNullOrWhiteSpace(admin.ProfileImageUrl))
            {
                try
                {
                    var oldUrl = admin.ProfileImageUrl;
                    var qIdx = oldUrl.IndexOf('?');
                    var cleanOld = qIdx >= 0 ? oldUrl.Substring(0, qIdx) : oldUrl;
                    await blobStorageService.DeleteAsync(cleanOld);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[AdminMgmt] Avatar blob silinemedi | ActorId: {ActorId}", actingAdminId);
                }
            }

            admin.ProfileImageUrl = null;
            admin.UpdatedAt = DateTime.UtcNow;
            await adminUserDal.Update(admin);

            await auditService.RecordAsync(AuditAction.AdminProfileUpdated, actingAdminId, actingAdminId, null, true, "AvatarRemoved");
            return new SuccessDataResult<AdminUserListItemDto>(Map(admin), Messages.AdminMgmtProfileUpdated);
        }

        private static AdminUserListItemDto Map(AdminUser a) => new()
        {
            Id = a.Id,
            Email = a.Email,
            FullName = a.FullName,
            IsActive = a.IsActive,
            ProfileImageUrl = a.ProfileImageUrl,
            LastLoginAt = a.LastLoginAt,
            CreatedAt = a.CreatedAt,
            UpdatedAt = a.UpdatedAt
        };
    }
}
