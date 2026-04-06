
using Business.Abstract;
using Business.Resources;
using Business.ValidationRules.FluentValidation;
using Business.BusinessAspect.Autofac;
using Core.Aspect.Autofac.Logging;
using Core.Aspect.Autofac.Validation;
using Core.Utilities.Results;
using Core.Utilities.Security.JWT;
using Core.Utilities.Security.PhoneSetting;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.Extensions.Configuration;
using Core.Aspect.Autofac.Transaction;

namespace Business.Concrete
{
    public class UserManager(
        IUserDal userDal,
        IPhoneService phoneService,
        ITokenHelper tokenHelper,
        IImageService imageService,
        IRefreshTokenService refreshTokenService,
        IRefreshTokenDal refreshTokenDal,
        IConfiguration configuration,
        IOperationClaimDal operationClaimDal,
        IUserOperationClaimService userOperationClaimService,
        ISmsVerifyService smsVerifyService,
        IBarberStoreService barberStoreService,
        IFreeBarberService freeBarberService,
        IAppointmentService appointmentService,
        IComplaintService complaintService,
        IChatService chatService,
        IFavoriteDal favoriteDal,
        INotificationDal notificationDal,
        IBlockedDal blockedDal,
        ISavedFilterDal savedFilterDal,
        IRequestDal requestDal,
        IUserFcmTokenDal userFcmTokenDal,
        IRatingDal ratingDal,
        IAuditService auditService) : IUserService
    {
        [LogAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IResult> Add(User user)
        {
            if (!string.IsNullOrWhiteSpace(user.PhoneNumber))
            {
                var e164 = phoneService.NormalizeToE164(user.PhoneNumber);
                user.PhoneNumber = string.Empty; // Plain text saklanmıyor
                user.PhoneNumberHash = phoneService.HashForLookup(e164);
                user.PhoneNumberEncrypted = phoneService.EncryptForStorage(e164);
            }
            if (!string.IsNullOrWhiteSpace(user.FirstName) && string.IsNullOrWhiteSpace(user.FirstNameEncrypted))
                user.FirstNameEncrypted = phoneService.EncryptForStorage(user.FirstName);
            if (!string.IsNullOrWhiteSpace(user.LastName) && string.IsNullOrWhiteSpace(user.LastNameEncrypted))
                user.LastNameEncrypted = phoneService.EncryptForStorage(user.LastName);
            await userDal.Add(user);
            
            // Kullanıcıya UserType'a göre rol ata
            await AssignRoleToUserAsync(user);
            
            return new SuccessResult("Kullanıcı Eklendi");
        }
        
        private async Task AssignRoleToUserAsync(User user)
        {
            var rolesToAssign = new List<string>();
            
            // UserType'a göre spesifik rol ver
            // Not: "User" rolü kaldırıldı - her kullanıcı zaten Customer, FreeBarber veya BarberStore rolüne sahip
            switch (user.UserType)
            {
                case UserType.Customer:
                    rolesToAssign.Add("Customer");
                    break;
                case UserType.FreeBarber:
                    rolesToAssign.Add("FreeBarber");
                    break;
                case UserType.BarberStore:
                    rolesToAssign.Add("BarberStore");
                    break;
            }
            
            // Kullanıcının mevcut rollerini kontrol et
            var existingClaimsResult = await userOperationClaimService.GetClaimByUserId(user.Id);
            var existingClaimIds = new HashSet<Guid>();
            
            if (existingClaimsResult.Success && existingClaimsResult.Data != null)
            {
                existingClaimIds = existingClaimsResult.Data.Select(uoc => uoc.OperationClaimId).ToHashSet();
            }
            
            // Rolleri veritabanından bul ve ata
            var userOperationClaims = new List<UserOperationClaim>();
            
            foreach (var roleName in rolesToAssign)
            {
                // Rolü veritabanından bul veya oluştur
                var operationClaim = await operationClaimDal.Get(oc => oc.Name == roleName);
                
                if (operationClaim == null)
                {
                    // Rol veritabanında yoksa oluştur
                    operationClaim = new OperationClaim { Name = roleName };
                    await operationClaimDal.Add(operationClaim);
                }
                
                if (operationClaim != null && !existingClaimIds.Contains(operationClaim.Id))
                {
                    userOperationClaims.Add(new UserOperationClaim
                    {
                        Id = Guid.NewGuid(),
                        UserId = user.Id,
                        OperationClaimId = operationClaim.Id
                    });
                }
            }
            
            // Rolleri ata (varsa)
            if (userOperationClaims.Any())
            {
                await userOperationClaimService.AddUserOperationsClaim(userOperationClaims);
            }
        }
        public async Task<IDataResult<User>> GetByPhone(string phoneNumber)
        {
            var user = await userDal.GetByPhone(phoneNumber);
            return new SuccessDataResult<User>(user);
        }

        public async Task<IDataResult<List<User>>> GetByPhoneAll(string phoneNumber)
        {
            var users = await userDal.GetByPhoneAll(phoneNumber);
            return new SuccessDataResult<List<User>>(users);
        }

        public async Task<IDataResult<User>> GetByCustomerNumber(string customerNumber)
        {
            var user = await userDal.GetByCustomerNumber(customerNumber);
            return new SuccessDataResult<User>(user);
        }

        public async Task<IDataResult<List<User>>> GetByCustomerNumberAll(string customerNumber)
        {
            var users = await userDal.GetByCustomerNumberAll(customerNumber);
            return new SuccessDataResult<List<User>>(users);
        }

        public async Task<IDataResult<List<OperationClaim>>> GetClaims(User user)
        {
            var claims = await userDal.GetClaims(user);
            return new SuccessDataResult<List<OperationClaim>>(claims);
        }

        public async Task<IDataResult<User>> GetById(Guid id)
        {
            var user = await userDal.Get(u => u.Id == id);
            return new SuccessDataResult<User>(user);
        }

        public async Task<IDataResult<User>> GetByName(string firstName, string lastName)
        {
            var user = await userDal.Get(u => u.FirstName == firstName && u.LastName == lastName);
            return new SuccessDataResult<User>(user);
        }

        [LogAspect]
        public async Task<IResult> Update(User user)
        {
            user.UpdatedAt = DateTime.UtcNow;
            await userDal.Update(user);
            return new SuccessResult("Kullanıcı güncellendi");
        }

        public async Task<IDataResult<UserProfileDto>> GetMe(Guid userId)
        {
            var user = await userDal.Get(u => u.Id == userId);
            if (user == null)
                return new ErrorDataResult<UserProfileDto>("Kullanıcı bulunamadı");

            // Tercihen encrypted alandan çöz, fallback legacy PhoneNumber
            var phone = phoneService.DecryptForRead(user.PhoneNumberEncrypted);

            // Get user image if exists
            ImageGetDto imageDto = null;
            if (user.ImageId.HasValue)
            {
                var imageResult = await imageService.GetImage(user.ImageId.Value);
                if (imageResult.Success && imageResult.Data != null)
                {
                    imageDto = imageResult.Data;
                }
            }

            var userProfile = new UserProfileDto
            {
                Id = user.Id,
                FirstName = phoneService.DecryptForRead(user.FirstNameEncrypted) ?? user.FirstName,
                LastName = phoneService.DecryptForRead(user.LastNameEncrypted) ?? user.LastName,
                PhoneNumber = phone,
                UserType = user.UserType,
                CustomerNumber = user.CustomerNumber,
                ImageId = user.ImageId,
                Image = imageDto,
                IsActive = user.IsActive,
                IsKvkkApproved = user.IsKvkkApproved
            };

            return new SuccessDataResult<UserProfileDto>(userProfile, "Kullanıcı bilgileri getirildi");
        }

        [SecuredOperation("Admin")]
        [LogAspect]
        public async Task<IDataResult<List<UserAdminGetDto>>> GetAllUsersForAdminAsync()
        {
            var users = await userDal.GetAll();
            var dtos = users.Select(u => new UserAdminGetDto
            {
                Id = u.Id,
                FirstName = phoneService.DecryptForRead(u.FirstNameEncrypted) ?? u.FirstName,
                LastName = phoneService.DecryptForRead(u.LastNameEncrypted) ?? u.LastName,
                PhoneNumber = phoneService.DecryptForRead(u.PhoneNumberEncrypted),
                UserType = u.UserType,
                IsActive = u.IsActive,
                IsBanned = u.IsBanned,
                BanReason = u.BanReason,
                CustomerNumber = u.CustomerNumber,
                ImageId = u.ImageId,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt
            }).ToList();

            return new SuccessDataResult<List<UserAdminGetDto>>(dtos);
        }

        [LogAspect]
        [ValidationAspect(typeof(UpdateUserDtoValidator))]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IDataResult<AccessToken>> UpdateProfile(UpdateUserDto dto, Guid currentUserId)
        {
            // Get current user
            var currentUserResult = await GetById(currentUserId);
            if (currentUserResult.Data == null)
            {
                return new ErrorDataResult<AccessToken>("Kullanıcı bulunamadı");
            }

            var currentUser = currentUserResult.Data;

            // Telefon alanı bu endpoint'ten güncellenmez — OTP doğrulamalı UpdatePhoneAsync kullanılmalı
            // Update user fields
            currentUser.FirstName = dto.FirstName;
            currentUser.FirstNameEncrypted = !string.IsNullOrWhiteSpace(dto.FirstName)
                ? phoneService.EncryptForStorage(dto.FirstName) : currentUser.FirstNameEncrypted;
            currentUser.LastName = dto.LastName;
            currentUser.LastNameEncrypted = !string.IsNullOrWhiteSpace(dto.LastName)
                ? phoneService.EncryptForStorage(dto.LastName) : currentUser.LastNameEncrypted;

            // Update user
            var updateResult = await Update(currentUser);
            if (!updateResult.Success)
            {
                return new ErrorDataResult<AccessToken>(updateResult.Message);
            }

            // Revoke all active refresh tokens for this user (güvenlik için)
            var activeTokens = await refreshTokenDal.GetActiveByUser(currentUserId);
            foreach (var token in activeTokens)
            {
                token.RevokedAt = DateTime.UtcNow;
                token.RevokedByIp = null; // Could be passed from controller if needed
                await refreshTokenDal.Update(token);
            }

            // Generate new access token with updated claims
            var claims = await GetClaims(currentUser);
            var newAccessToken = tokenHelper.CreateToken(currentUser, claims.Data);

            // Create new refresh token (like in AuthManager)
            var refreshDays = configuration.GetSection("TokenOptions:RefreshTokenExpirationDays").Get<int?>() ?? 30;
            var rt = refreshTokenService.CreateNew(refreshDays);
            var familyId = Guid.NewGuid();

            var refreshTokenEntity = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = currentUser.Id,
                TokenHash = rt.Hash,
                TokenSalt = rt.Salt,
                Fingerprint = rt.Fingerprint,
                FamilyId = familyId,
                CreatedAt = DateTime.UtcNow,
                CreatedByIp = null, // Could be passed from controller if needed
                Device = null, // Could be passed from controller if needed
                ExpiresAt = rt.Expires
            };

            await refreshTokenDal.Add(refreshTokenEntity);

            return new SuccessDataResult<AccessToken>(new AccessToken
            {
                Token = newAccessToken.Token,
                Expiration = newAccessToken.Expiration,
                RefreshToken = rt.Plain,
                RefreshTokenExpires = rt.Expires
            }, "Profil başarıyla güncellendi");
        }

        [LogAspect(logParameters: false)]
        public async Task<IResult> SendPhoneChangeOtpAsync(Guid currentUserId, string newPhone, string? language = null)
        {
            var e164 = phoneService.NormalizeToE164(newPhone);
            if (string.IsNullOrWhiteSpace(e164))
                return new ErrorResult("Geçersiz telefon numarası.");

            var currentUserResult = await GetById(currentUserId);
            if (currentUserResult.Data == null)
                return new ErrorResult("Kullanıcı bulunamadı.");

            var currentPhone = phoneService.DecryptForRead(currentUserResult.Data.PhoneNumberEncrypted);
            if (currentPhone == e164)
                return new ErrorResult("Girilen numara mevcut numaranızla aynı.");

            // Yeni numara başka kullanıcıya ait mi?
            var existing = await GetByPhone(e164);
            if (existing.Data != null && existing.Data.Id != currentUserId)
                return new ErrorResult("Bu telefon numarası başka bir kullanıcı tarafından kullanılıyor.");

            return await smsVerifyService.SendAsync(e164, language);
        }

        [LogAspect(logParameters: false)]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IDataResult<AccessToken>> UpdatePhoneAsync(Guid currentUserId, string newPhone, string otpCode)
        {
            var e164 = phoneService.NormalizeToE164(newPhone);

            // OTP doğrula
            var verifyResult = await smsVerifyService.CheckAsync(e164, otpCode);
            if (!verifyResult.Success)
                return new ErrorDataResult<AccessToken>(verifyResult.Message);

            var currentUserResult = await GetById(currentUserId);
            if (currentUserResult.Data == null)
                return new ErrorDataResult<AccessToken>("Kullanıcı bulunamadı.");

            var currentUser = currentUserResult.Data;

            // Aynı müşteri numarasına sahip tüm kullanıcıların telefonunu güncelle
            if (!string.IsNullOrEmpty(currentUser.CustomerNumber))
            {
                var siblings = await GetByCustomerNumberAll(currentUser.CustomerNumber);
                if (siblings.Data != null)
                {
                    foreach (var sibling in siblings.Data)
                    {
                        sibling.PhoneNumber = string.Empty; // Plain text temizleniyor
                        sibling.PhoneNumberHash = phoneService.HashForLookup(e164);
                        sibling.PhoneNumberEncrypted = phoneService.EncryptForStorage(e164);
                        sibling.UpdatedAt = DateTime.UtcNow;
                        await userDal.Update(sibling);
                    }
                }
            }
            else
            {
                currentUser.PhoneNumber = string.Empty; // Plain text temizleniyor
                currentUser.PhoneNumberHash = phoneService.HashForLookup(e164);
                currentUser.PhoneNumberEncrypted = phoneService.EncryptForStorage(e164);
                currentUser.UpdatedAt = DateTime.UtcNow;
                await userDal.Update(currentUser);
            }

            // Güvenlik: mevcut refresh token'ları iptal et
            var activeTokens = await refreshTokenDal.GetActiveByUser(currentUserId);
            foreach (var token in activeTokens)
            {
                token.RevokedAt = DateTime.UtcNow;
                await refreshTokenDal.Update(token);
            }

            // Yeni token üret
            var claims = await GetClaims(currentUser);
            var newAccessToken = tokenHelper.CreateToken(currentUser, claims.Data);
            var refreshDays = configuration.GetSection("TokenOptions:RefreshTokenExpirationDays").Get<int?>() ?? 30;
            var rt = refreshTokenService.CreateNew(refreshDays);
            await refreshTokenDal.Add(new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = currentUser.Id,
                TokenHash = rt.Hash,
                TokenSalt = rt.Salt,
                Fingerprint = rt.Fingerprint,
                FamilyId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = rt.Expires
            });

            return new SuccessDataResult<AccessToken>(new AccessToken
            {
                Token = newAccessToken.Token,
                Expiration = newAccessToken.Expiration,
                RefreshToken = rt.Plain,
                RefreshTokenExpires = rt.Expires
            }, "Telefon numarası başarıyla güncellendi.");
        }

        public async Task<IResult> SendDeleteAccountOtpAsync(Guid userId, string? language = null)
        {
            var userResult = await GetById(userId);
            if (userResult.Data == null)
                return new ErrorResult("Kullanıcı bulunamadı.");

            var e164 = phoneService.DecryptForRead(userResult.Data.PhoneNumberEncrypted);
            if (string.IsNullOrWhiteSpace(e164))
                return new ErrorResult("Telefon numarası bulunamadı.");

            return await smsVerifyService.SendAsync(e164, language);
        }

        [LogAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IResult> DeleteAccountAsync(Guid userId, string otpCode)
        {
            var userResult = await GetById(userId);
            if (userResult.Data == null)
                return new ErrorResult("Kullanıcı bulunamadı.");

            var user = userResult.Data;

            // OTP doğrula
            var e164 = phoneService.DecryptForRead(user.PhoneNumberEncrypted);
            if (string.IsNullOrWhiteSpace(e164))
                return new ErrorResult("Telefon numarası bulunamadı.");

            var verifyResult = await smsVerifyService.CheckAsync(e164, otpCode);
            if (!verifyResult.Success)
                return verifyResult;

            var blocking = await appointmentService.AnyBlockingAppointmentForUserAsync(userId);
            if (!blocking.Success)
                return new ErrorResult(blocking.Message);
            if (blocking.Data)
                return new ErrorResult(Messages.AccountDeleteBlockedByActiveAppointments);

            // Dükkan(lar)ı ve bağlı tüm verileri sil (blocking check dahil)
            var storeDeleteResult = await barberStoreService.DeleteByUserIdAsync(userId);
            if (!storeDeleteResult.Success)
                return storeDeleteResult;

            // Free barber panelini ve bağlı verileri sil (blocking check dahil)
            var panelDeleteResult = await freeBarberService.DeleteByUserIdAsync(userId);
            if (!panelDeleteResult.Success)
                return panelDeleteResult;

            // Favoriler: kullanıcının eklediği ve kullanıcıya eklenen tüm favoriler
            var favorites = await favoriteDal.GetAll(x => x.FavoritedFromId == userId || x.FavoritedToId == userId);
            if (favorites.Count > 0)
                await favoriteDal.DeleteAll(favorites);

            // Bildirimler
            var notifications = await notificationDal.GetAll(x => x.UserId == userId);
            if (notifications.Count > 0)
                await notificationDal.DeleteAll(notifications);

            // Engellenenler: hem engeller hem engellenir taraf
            var blocked = await blockedDal.GetAll(x => x.BlockedFromUserId == userId || x.BlockedToUserId == userId);
            if (blocked.Count > 0)
                await blockedDal.DeleteAll(blocked);

            // Kayıtlı filtreler
            var savedFilters = await savedFilterDal.GetAll(x => x.UserId == userId);
            if (savedFilters.Count > 0)
                await savedFilterDal.DeleteAll(savedFilters);

            // İstekler (destek/istek bildirimleri)
            var requests = await requestDal.GetAll(x => x.RequestFromUserId == userId);
            if (requests.Count > 0)
                await requestDal.DeleteAll(requests);

            // FCM Token'ları
            var fcmTokens = await userFcmTokenDal.GetAll(x => x.UserId == userId);
            if (fcmTokens.Count > 0)
                await userFcmTokenDal.DeleteAll(fcmTokens);

            // Kullanıcı rating'leri: müşteri olarak verdiği ve müşteri olarak aldığı
            var ratings = await ratingDal.GetAll(x => x.RatedFromId == userId || x.TargetId == userId);
            if (ratings.Count > 0)
                await ratingDal.DeleteAll(ratings);

            await complaintService.SoftDeleteAllInvolvingUserForAccountClosureAsync(userId);
            await chatService.RedactUserContentForAccountClosureAsync(userId);

            var removeClaims = await userOperationClaimService.RemoveAllClaimsForUserAsync(userId);
            if (!removeClaims.Success)
                return removeClaims;

            // Kullanıcı profil resmi
            if (user.ImageId.HasValue)
                await imageService.DeleteAsync(user.ImageId.Value, userId);

            // Kişisel verileri anonimize et (HelpGuide / kategori / OperationClaim master kayıtlarına dokunulmaz)
            user.FirstName = "Silindi";
            user.LastName = "Silindi";
            user.FirstNameEncrypted = null;
            user.LastNameEncrypted = null;
            user.PhoneNumber = string.Empty;
            user.PhoneNumberHash = null;
            user.PhoneNumberEncrypted = null;
            user.ImageId = null;
            user.IsActive = false;
            // KVKK onayı hesap kapalı olduğunda sıfırlanır; hukuki saklama yükümlülükleri ayrı değerlendirilmelidir
            user.IsKvkkApproved = false;
            user.KvkkApprovedAt = null;
            user.UpdatedAt = DateTime.UtcNow;

            await userDal.Update(user);

            // Tüm refresh token'ları iptal et
            var activeTokens = await refreshTokenDal.GetActiveByUser(userId);
            foreach (var token in activeTokens)
            {
                token.RevokedAt = DateTime.UtcNow;
                await refreshTokenDal.Update(token);
            }

            await auditService.RecordAsync(AuditAction.AccountClosed, userId, userId, null, true);

            return new SuccessResult("Hesabınız başarıyla silindi.");
        }

        [LogAspect]
        public async Task<IResult> CompleteHelpGuidePromptAsync(Guid userId)
        {
            var user = await userDal.Get(u => u.Id == userId);
            if (user == null)
                return new ErrorResult("Kullanıcı bulunamadı.");
            if (user.HelpGuidePromptCompleted)
                return new SuccessResult();
            user.HelpGuidePromptCompleted = true;
            user.UpdatedAt = DateTime.UtcNow;
            await userDal.Update(user);
            return new SuccessResult();
        }
    }
}
