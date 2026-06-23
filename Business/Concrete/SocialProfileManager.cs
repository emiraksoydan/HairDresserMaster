using System;
using System.Text.RegularExpressions;
using Business.Abstract;
using Business.BusinessAspect.Autofac;
using Business.Helpers;
using Core.Aspect.Autofac.Logging;
using Core.Utilities.Results;
using Core.Utilities.Helpers;
using Core.Utilities.Security.PhoneSetting;
using Core.Utilities.Storage;
using DataAccess.Abstract;
using Entities.Concrete.Constants;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Business.Concrete
{
    public class SocialProfileManager(
        ISocialProfileDal socialProfileDal,
        ISocialFollowDal socialFollowDal,
        ISocialProfileMuteDal socialProfileMuteDal,
        IImageDal imageDal,
        IUserDal userDal,
        IFreeBarberDal freeBarberDal,
        IBarberStoreDal barberStoreDal,
        IRatingDal ratingDal,
        IPhoneService phoneService,
        IBlobStorageService blobStorageService,
        BlockedHelper blockedHelper,
        ILogger<SocialProfileManager> logger) : ISocialProfileService
    {
        private const string AvatarContainer = "social-avatars";
        private const string CoverContainer = "social-covers";
        private static readonly Regex UsernameSanitizer = new(@"[^a-z0-9_]", RegexOptions.Compiled);
        private static readonly Regex UsernamePattern = new(@"^[a-z0-9_]{3,30}$", RegexOptions.Compiled);

        [LogAspect]
        public async Task<IResult> EnsureCustomerProfileAsync(Guid userId, string displayName)
        {
            return await EnsureProfileInternalAsync(
                SocialProfileOwnerType.Customer,
                userId,
                userId,
                displayName,
                null,
                null);
        }

        [LogAspect]
        public async Task<IResult> EnsureFreeBarberProfileAsync(
            Guid freeBarberId, Guid userId, string displayName, double latitude, double longitude)
        {
            return await EnsureProfileInternalAsync(
                SocialProfileOwnerType.FreeBarber,
                freeBarberId,
                userId,
                displayName,
                latitude,
                longitude);
        }

        [LogAspect]
        public async Task<IResult> EnsureStoreProfileAsync(
            Guid storeId, Guid userId, string storeName, double latitude, double longitude)
        {
            return await EnsureProfileInternalAsync(
                SocialProfileOwnerType.BarberStore,
                storeId,
                userId,
                storeName,
                latitude,
                longitude);
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IDataResult<List<SocialProfileDto>>> GetMyProfilesAsync(Guid userId)
        {
            var profiles = await socialProfileDal.GetByUserIdAsync(userId);
            var dtos = new List<SocialProfileDto>();
            foreach (var p in profiles)
                dtos.Add(await MapToDtoAsync(p, userId));
            return new SuccessDataResult<List<SocialProfileDto>>(dtos);
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IDataResult<SocialProfileDto>> GetProfileAsync(
            Guid profileId, Guid? viewerUserId, double? viewerLatitude = null, double? viewerLongitude = null)
        {
            var profile = await socialProfileDal.Get(p => p.Id == profileId);
            if (profile == null || profile.Status != SocialContentStatus.Active)
                return new ErrorDataResult<SocialProfileDto>(SocialErrorCodes.ProfileNotFound);

            return new SuccessDataResult<SocialProfileDto>(
                await MapToDtoAsync(profile, viewerUserId, viewerLatitude, viewerLongitude));
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IDataResult<SocialProfileDto>> GetProfileByOwnerAsync(
            SocialProfileOwnerType ownerType, Guid ownerId, Guid? viewerUserId,
            double? viewerLatitude = null, double? viewerLongitude = null)
        {
            var profile = await socialProfileDal.GetByOwnerAsync(ownerType, ownerId);
            if (profile == null || profile.Status != SocialContentStatus.Active)
                return new ErrorDataResult<SocialProfileDto>(SocialErrorCodes.ProfileNotFound);

            return new SuccessDataResult<SocialProfileDto>(
                await MapToDtoAsync(profile, viewerUserId, viewerLatitude, viewerLongitude));
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IDataResult<SocialProfileDto>> GetProfileByUsernameAsync(
            string username, Guid? viewerUserId)
        {
            var profile = await socialProfileDal.GetByUsernameAsync(username);
            if (profile == null || profile.Status != SocialContentStatus.Active)
                return new ErrorDataResult<SocialProfileDto>(SocialErrorCodes.ProfileNotFound);

            if (viewerUserId.HasValue &&
                await blockedHelper.HasBlockBetweenAsync(viewerUserId.Value, profile.UserId))
                return new ErrorDataResult<SocialProfileDto>(SocialErrorCodes.ProfileNoAccess);

            return new SuccessDataResult<SocialProfileDto>(
                await MapToDtoAsync(profile, viewerUserId));
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IDataResult<List<SocialProfileSearchResultDto>>> SearchProfilesAsync(
            Guid userId,
            string? query,
            double? latitude,
            double? longitude,
            double radiusKm,
            int limit,
            AvailabilityFilter? availability = null,
            IReadOnlyList<Guid>? serviceIds = null)
        {
            limit = Math.Clamp(limit, 1, 50);
            radiusKm = SocialDiscoverFilterHelper.NormalizeRadiusKm(radiusKm);

            var hasQuery = !string.IsNullOrWhiteSpace(query);
            var hasGeo = latitude.HasValue && longitude.HasValue;
            var hasAvailabilityFilter = availability.HasValue && availability.Value != AvailabilityFilter.Any;
            var hasServiceFilter = serviceIds != null && serviceIds.Count > 0;
            if (!hasQuery && !hasGeo && !hasAvailabilityFilter && !hasServiceFilter)
                return new ErrorDataResult<List<SocialProfileSearchResultDto>>(SocialErrorCodes.SearchNeedInput);

            var blocked = await blockedHelper.GetAllBlockedUserIdsAsync(userId);
            var rows = await socialProfileDal.SearchProfilesAsync(
                query, latitude, longitude, radiusKm, blocked, userId, limit, availability, serviceIds);

            var viewerProfileIds = (await socialProfileDal.GetByUserIdAsync(userId))
                .Select(p => p.Id)
                .ToList();

            var results = new List<SocialProfileSearchResultDto>();
            foreach (var (profile, distanceKm) in rows)
            {
                var dto = await MapToSearchResultAsync(profile, userId, viewerProfileIds, distanceKm);
                results.Add(dto);
            }

            return new SuccessDataResult<List<SocialProfileSearchResultDto>>(results);
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IResult> UpdateProfileAsync(Guid userId, Guid profileId, SocialProfileUpdateDto dto)
        {
            var profile = await socialProfileDal.Get(p => p.Id == profileId);
            if (profile == null)
                return new ErrorResult(SocialErrorCodes.ProfileNotFound);
            if (profile.UserId != userId)
                return new ErrorResult(SocialErrorCodes.ProfileNoPermission);

            if (dto.Username != null)
            {
                var username = NormalizeUsernameBase(dto.Username);
                if (!UsernamePattern.IsMatch(username))
                    return new ErrorResult(SocialErrorCodes.UsernameInvalid);
                if (!string.Equals(profile.Username, username, StringComparison.Ordinal)
                    && await socialProfileDal.UsernameExistsAsync(username))
                    return new ErrorResult(SocialErrorCodes.UsernameTaken);
                profile.Username = username;
            }

            if (dto.Bio != null)
                profile.Bio = string.IsNullOrWhiteSpace(dto.Bio) ? null : dto.Bio.Trim();
            if (dto.IsPrivate.HasValue)
                profile.IsPrivate = dto.IsPrivate.Value;

            if (dto.ExternalUrl != null)
            {
                var url = dto.ExternalUrl.Trim();
                if (string.IsNullOrEmpty(url))
                    profile.ExternalUrl = null;
                else if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) ||
                         (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
                    return new ErrorResult(SocialErrorCodes.ExternalUrlInvalid);
                else
                    profile.ExternalUrl = url;
            }

            if (dto.Latitude.HasValue && dto.Longitude.HasValue)
            {
                profile.Latitude = dto.Latitude.Value;
                profile.Longitude = dto.Longitude.Value;
            }

            if (dto.DmPolicy.HasValue)
                profile.DmPolicy = dto.DmPolicy.Value;

            profile.UpdatedAt = DateTime.UtcNow;
            await socialProfileDal.Update(profile);
            return new SuccessResult("Profil güncellendi.");
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IDataResult<SocialProfileDto>> UploadAvatarAsync(Guid userId, Guid profileId, IFormFile file)
        {
            var profile = await socialProfileDal.Get(p => p.Id == profileId);
            if (profile == null)
                return new ErrorDataResult<SocialProfileDto>(SocialErrorCodes.ProfileNotFound);
            if (profile.UserId != userId)
                return new ErrorDataResult<SocialProfileDto>(SocialErrorCodes.ProfileNoPermission);
            if (file == null || file.Length == 0)
                return new ErrorDataResult<SocialProfileDto>(SocialErrorCodes.AvatarRequired);

            var validation = UploadFileValidator.ValidateProfileOrOwnerImage(file);
            if (!validation.Success)
                return new ErrorDataResult<SocialProfileDto>(validation.Message);

            byte[] fileBytes;
            await using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms);
                fileBytes = ms.ToArray();
            }

            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(ext))
                ext = ".jpg";

            string url;
            try
            {
                url = await blobStorageService.UploadBytesAsync(
                    fileBytes, AvatarContainer, $"{Guid.NewGuid()}{ext}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Social] Avatar blob yüklemesi başarısız.");
                return new ErrorDataResult<SocialProfileDto>(SocialErrorCodes.AvatarUploadFailed);
            }

            if (string.IsNullOrWhiteSpace(url))
                return new ErrorDataResult<SocialProfileDto>(SocialErrorCodes.AvatarUploadFailed);
            var urlWithTimestamp = $"{url}?t={DateTime.UtcNow.Ticks}";

            var image = new Image
            {
                Id = Guid.NewGuid(),
                ImageUrl = urlWithTimestamp,
                OwnerType = ImageOwnerType.SocialProfile,
                ImageOwnerId = profileId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            await imageDal.Add(image);

            profile.AvatarImageId = image.Id;
            profile.UpdatedAt = DateTime.UtcNow;
            await socialProfileDal.Update(profile);

            return new SuccessDataResult<SocialProfileDto>(
                await MapToDtoAsync(profile, userId),
                "Profil fotoğrafı güncellendi.");
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IDataResult<SocialProfileDto>> UploadCoverAsync(Guid userId, Guid profileId, IFormFile file)
        {
            var profile = await socialProfileDal.Get(p => p.Id == profileId);
            if (profile == null)
                return new ErrorDataResult<SocialProfileDto>(SocialErrorCodes.ProfileNotFound);
            if (profile.UserId != userId)
                return new ErrorDataResult<SocialProfileDto>(SocialErrorCodes.ProfileNoPermission);
            if (file == null || file.Length == 0)
                return new ErrorDataResult<SocialProfileDto>(SocialErrorCodes.AvatarRequired);

            var validation = UploadFileValidator.ValidateProfileOrOwnerImage(file);
            if (!validation.Success)
                return new ErrorDataResult<SocialProfileDto>(validation.Message);

            byte[] fileBytes;
            await using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms);
                fileBytes = ms.ToArray();
            }

            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(ext))
                ext = ".jpg";

            string url;
            try
            {
                url = await blobStorageService.UploadBytesAsync(
                    fileBytes, CoverContainer, $"{Guid.NewGuid()}{ext}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Social] Kapak blob yüklemesi başarısız.");
                return new ErrorDataResult<SocialProfileDto>(SocialErrorCodes.CoverUploadFailed);
            }

            if (string.IsNullOrWhiteSpace(url))
                return new ErrorDataResult<SocialProfileDto>(SocialErrorCodes.CoverUploadFailed);
            var urlWithTimestamp = $"{url}?t={DateTime.UtcNow.Ticks}";

            var image = new Image
            {
                Id = Guid.NewGuid(),
                ImageUrl = urlWithTimestamp,
                OwnerType = ImageOwnerType.SocialProfile,
                ImageOwnerId = profileId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            await imageDal.Add(image);

            profile.CoverImageId = image.Id;
            profile.UpdatedAt = DateTime.UtcNow;
            await socialProfileDal.Update(profile);

            return new SuccessDataResult<SocialProfileDto>(
                await MapToDtoAsync(profile, userId),
                "Kapak fotoğrafı güncellendi.");
        }

        private async Task<IResult> EnsureProfileInternalAsync(
            SocialProfileOwnerType ownerType,
            Guid ownerId,
            Guid userId,
            string displayName,
            double? latitude,
            double? longitude)
        {
            var existing = await socialProfileDal.GetByOwnerAsync(ownerType, ownerId);
            if (existing != null)
                return new SuccessResult();

            var username = await GenerateUniqueUsernameAsync(displayName);
            var now = DateTime.UtcNow;
            var profile = new SocialProfile
            {
                Id = Guid.NewGuid(),
                OwnerType = ownerType,
                OwnerId = ownerId,
                UserId = userId,
                Username = username,
                Latitude = latitude,
                Longitude = longitude,
                IsPrivate = false,
                Status = SocialContentStatus.Active,
                CreatedAt = now,
                UpdatedAt = now,
            };

            await socialProfileDal.Add(profile);
            return new SuccessResult();
        }

        private async Task<string> GenerateUniqueUsernameAsync(string displayName)
        {
            var baseName = NormalizeUsernameBase(displayName);
            if (baseName.Length < 3)
                baseName = "kullanici";

            for (var attempt = 0; attempt < 20; attempt++)
            {
                var suffix = attempt == 0 ? string.Empty : $"_{Random.Shared.Next(1000, 99999)}";
                var candidate = (baseName + suffix).Trim('_');
                if (candidate.Length > 30)
                    candidate = candidate[..30].TrimEnd('_');

                if (!await socialProfileDal.UsernameExistsAsync(candidate))
                    return candidate;
            }

            return $"user_{Guid.NewGuid():N}"[..20];
        }

        private static string NormalizeUsernameBase(string displayName)
        {
            var normalized = displayName.Trim().ToLowerInvariant();
            normalized = normalized
                .Replace('ç', 'c').Replace('ğ', 'g').Replace('ı', 'i')
                .Replace('ö', 'o').Replace('ş', 's').Replace('ü', 'u');
            normalized = UsernameSanitizer.Replace(normalized, "_");
            normalized = Regex.Replace(normalized, "_+", "_").Trim('_');
            return normalized;
        }

        private async Task<SocialProfileDto> MapToDtoAsync(
            SocialProfile profile, Guid? viewerUserId, double? viewerLatitude = null, double? viewerLongitude = null)
        {
            var viewerProfileIds = viewerUserId.HasValue
                ? (await socialProfileDal.GetByUserIdAsync(viewerUserId.Value)).Select(p => p.Id).ToList()
                : null;
            return await MapToDtoCoreAsync(profile, viewerUserId, viewerProfileIds, viewerLatitude, viewerLongitude);
        }

        private async Task<SocialProfileSearchResultDto> MapToSearchResultAsync(
            SocialProfile profile,
            Guid viewerUserId,
            IReadOnlyList<Guid> viewerProfileIds,
            double? distanceKm)
        {
            var dto = await MapToDtoCoreAsync(profile, viewerUserId, viewerProfileIds);
            await EnrichOwnerNumberAsync(dto, profile);
            return new SocialProfileSearchResultDto
            {
                Id = dto.Id,
                OwnerType = dto.OwnerType,
                OwnerId = dto.OwnerId,
                UserId = dto.UserId,
                Username = dto.Username,
                Bio = dto.Bio,
                AvatarUrl = dto.AvatarUrl,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                IsPrivate = dto.IsPrivate,
                PostCount = dto.PostCount,
                FollowerCount = dto.FollowerCount,
                FollowingCount = dto.FollowingCount,
                IsFollowing = dto.IsFollowing,
                IsOwnProfile = dto.IsOwnProfile,
                OwnerNumber = dto.OwnerNumber,
                DistanceKm = distanceKm.HasValue ? Math.Round(distanceKm.Value, 1) : null,
            };
        }

        private async Task<SocialProfileDto> MapToDtoCoreAsync(
            SocialProfile profile,
            Guid? viewerUserId,
            IReadOnlyList<Guid>? viewerProfileIds,
            double? viewerLatitude = null,
            double? viewerLongitude = null)
        {
            string? avatarUrl = null;
            if (profile.AvatarImageId.HasValue)
            {
                var img = await imageDal.Get(i => i.Id == profile.AvatarImageId.Value);
                avatarUrl = img?.ImageUrl;
            }

            string? coverUrl = null;
            if (profile.CoverImageId.HasValue)
            {
                var coverImg = await imageDal.Get(i => i.Id == profile.CoverImageId.Value);
                coverUrl = coverImg?.ImageUrl;
            }

            viewerProfileIds ??= viewerUserId.HasValue
                ? (await socialProfileDal.GetByUserIdAsync(viewerUserId.Value)).Select(p => p.Id).ToList()
                : null;
            var stats = await socialProfileDal.GetStatsAsync(profile.Id, viewerProfileIds);
            var hasActiveStory = await socialProfileDal.HasActiveStoryAsync(profile.Id);

            var isOwn = viewerUserId.HasValue && profile.UserId == viewerUserId.Value;
            var dto = new SocialProfileDto
            {
                Id = profile.Id,
                OwnerType = profile.OwnerType,
                OwnerId = profile.OwnerId,
                UserId = profile.UserId,
                Username = profile.Username,
                Bio = profile.Bio,
                ExternalUrl = profile.ExternalUrl,
                AvatarUrl = avatarUrl,
                CoverUrl = coverUrl,
                DmPolicy = profile.DmPolicy,
                HasActiveStory = hasActiveStory,
                Latitude = profile.Latitude,
                Longitude = profile.Longitude,
                IsPrivate = profile.IsPrivate,
                PostCount = stats.PostCount,
                FollowerCount = stats.FollowerCount,
                FollowingCount = stats.FollowingCount,
                IsFollowing = stats.IsFollowing,
                IsOwnProfile = isOwn,
            };

            if (isOwn)
            {
                await EnrichOwnerInfoAsync(dto, profile);
                dto.TotalPostViews = await socialProfileDal.GetTotalPostViewsAsync(profile.Id);
                dto.HighlightCount = await socialProfileDal.GetHighlightCountAsync(profile.Id);
                dto.ReelCount = await socialProfileDal.GetReelCountAsync(profile.Id);
            }
            else if (viewerProfileIds is { Count: > 0 })
            {
                dto.MutualFollowerCount = await socialFollowDal.CountMutualFollowersAsync(viewerProfileIds, profile.Id);
                dto.IsMuted = await socialProfileMuteDal.IsMutedByAnyAsync(viewerProfileIds, profile.Id);
            }

            await EnrichRatingAsync(dto, profile);
            await EnrichAvailabilityAsync(dto, profile);

            if (viewerLatitude.HasValue && viewerLongitude.HasValue &&
                profile.Latitude.HasValue && profile.Longitude.HasValue)
            {
                dto.DistanceKm = Math.Round(
                    Geo.DistanceKm(viewerLatitude.Value, viewerLongitude.Value, profile.Latitude.Value, profile.Longitude.Value),
                    1);
            }

            return dto;
        }

        private async Task EnrichRatingAsync(SocialProfileDto dto, SocialProfile profile)
        {
            if (profile.OwnerType == SocialProfileOwnerType.Customer)
                return;

            Guid ratingTargetId = profile.OwnerType switch
            {
                SocialProfileOwnerType.BarberStore => profile.OwnerId,
                SocialProfileOwnerType.FreeBarber => (await freeBarberDal.Get(f => f.Id == profile.OwnerId))?.FreeBarberUserId ?? Guid.Empty,
                _ => Guid.Empty,
            };

            if (ratingTargetId == Guid.Empty)
                return;

            var ratings = await ratingDal.GetAll(r => r.TargetId == ratingTargetId && !r.IsHidden);
            if (ratings.Count == 0)
                return;

            dto.AverageRating = Math.Round(ratings.Average(r => r.Score), 1);
            dto.RatingCount = ratings.Count;
        }

        private async Task EnrichOwnerNumberAsync(SocialProfileDto dto, SocialProfile profile)
        {
            switch (profile.OwnerType)
            {
                case SocialProfileOwnerType.Customer:
                {
                    var user = await userDal.Get(u => u.Id == profile.OwnerId);
                    dto.OwnerNumber = user?.CustomerNumber;
                    break;
                }
                case SocialProfileOwnerType.FreeBarber:
                {
                    var fb = await freeBarberDal.Get(f => f.Id == profile.OwnerId);
                    if (fb == null) return;
                    var fbUser = await userDal.Get(u => u.Id == fb.FreeBarberUserId);
                    dto.OwnerNumber = fbUser?.CustomerNumber;
                    break;
                }
                case SocialProfileOwnerType.BarberStore:
                {
                    var store = await barberStoreDal.Get(s => s.Id == profile.OwnerId);
                    dto.OwnerNumber = store?.StoreNo;
                    break;
                }
            }
        }

        private async Task EnrichOwnerInfoAsync(SocialProfileDto dto, SocialProfile profile)
        {
            switch (profile.OwnerType)
            {
                case SocialProfileOwnerType.Customer:
                {
                    var user = await userDal.Get(u => u.Id == profile.OwnerId);
                    if (user == null) return;
                    var first = phoneService.DecryptForRead(user.FirstNameEncrypted) ?? user.FirstName;
                    var last = phoneService.DecryptForRead(user.LastNameEncrypted) ?? user.LastName;
                    dto.OwnerDisplayName = $"{first} {last}".Trim();
                    dto.OwnerNumber = user.CustomerNumber;
                    break;
                }
                case SocialProfileOwnerType.FreeBarber:
                {
                    var fb = await freeBarberDal.Get(f => f.Id == profile.OwnerId);
                    if (fb == null) return;
                    dto.OwnerDisplayName = $"{fb.FirstName} {fb.LastName}".Trim();
                    dto.OwnerBarberType = fb.Type;
                    var fbUser = await userDal.Get(u => u.Id == fb.FreeBarberUserId);
                    dto.OwnerNumber = fbUser?.CustomerNumber;
                    break;
                }
                case SocialProfileOwnerType.BarberStore:
                {
                    var store = await barberStoreDal.Get(s => s.Id == profile.OwnerId);
                    if (store == null) return;
                    dto.OwnerDisplayName = store.StoreName;
                    dto.OwnerNumber = store.StoreNo;
                    dto.OwnerBarberType = store.Type;
                    break;
                }
            }
        }

        private async Task EnrichAvailabilityAsync(SocialProfileDto dto, SocialProfile profile)
        {
            if (profile.OwnerType != SocialProfileOwnerType.FreeBarber)
                return;

            var fb = await freeBarberDal.Get(f => f.Id == profile.OwnerId);
            if (fb != null)
                dto.IsAvailable = fb.IsAvailable;
        }
    }
}
