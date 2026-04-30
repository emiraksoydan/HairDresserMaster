using Business.Abstract;
using Business.Helpers;
using Business.Resources;
using Core.Aspect.Autofac.Logging;
using Core.Aspect.Autofac.Transaction;
using Core.Utilities.Results;
using Core.Utilities.Storage;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Mapster;
using Microsoft.AspNetCore.Http;

namespace Business.Concrete
{
    public class ImageManager(
        IImageDal _imageDal,
        IBlobStorageService _blobStorageService,
        IUserDal _userDal,
        IBarberStoreDal _barberStoreDal,
        IFreeBarberDal _freeBarberDal,
        IManuelBarberDal _manuelBarberDal,
        IRealTimePublisher _realTimePublisher,
        IContentModerationService _contentModerationService) : IImageService
    {
        /// <summary>
        /// Azure Content Safety bazı formatları (HEIC/HEIF) desteklemez; ses dosyaları da görsel API'ye gönderilmez.
        /// </summary>
        private static bool ShouldSkipImageModeration(string? contentType, string? fileName)
        {
            if (!string.IsNullOrEmpty(contentType) &&
                contentType.StartsWith("audio/", System.StringComparison.OrdinalIgnoreCase))
                return true;
            if (!string.IsNullOrEmpty(contentType) &&
                (contentType.Equals("image/heic", System.StringComparison.OrdinalIgnoreCase) ||
                 contentType.Equals("image/heif", System.StringComparison.OrdinalIgnoreCase)))
                return true;
            if (!string.IsNullOrEmpty(fileName))
            {
                var fn = fileName.ToLowerInvariant();
                if (fn.EndsWith(".heic") || fn.EndsWith(".heif"))
                    return true;
            }
            return false;
        }

        public async Task<IResult> DeleteAsync(Guid id, Guid currentUserId)
        {
            var getImage = await _imageDal.Get(i => i.Id == id);
            if (getImage == null)
                return new ErrorResult("Resim bulunamadı.");

            var auth = await EnsureCurrentUserCanMutateExistingImageAsync(getImage, currentUserId);
            if (!auth.Success)
                return auth;

            if (!string.IsNullOrEmpty(getImage.ImageUrl))
                await _blobStorageService.DeleteAsync(getImage.ImageUrl);

            await _imageDal.Remove(getImage);
            return new SuccessResult();
        }

        public async Task<IDataResult<ImageGetDto>> GetImage(Guid id)
        {
            var image = await _imageDal.Get(x => x.Id == id);
            if (image == null)
                return new ErrorDataResult<ImageGetDto>("Resim bulunamadı.");

            var dto = image.Adapt<ImageGetDto>();

            return new SuccessDataResult<ImageGetDto>(dto);
        }

        [LogAspect]
        public async Task<IDataResult<string>> UploadImageAsync(IFormFile file, ImageOwnerType ownerType, Guid ownerId, Guid currentUserId, bool updateProfileImage = true)
        {
            var uploadAuth = await EnsureCurrentUserCanUploadAsync(ownerType, ownerId, currentUserId);
            if (!uploadAuth.Success)
                return new ErrorDataResult<string>(uploadAuth.Message);

            // B4: MIME allow-list + magic-byte + max boyut + filename sanitize.
            // Profil/galeri/sertifika uploadları (updateProfileImage=true VEYA OwnerType≠User)
            // sadece resim kabul eder; chat medyası (User + isProfileImage=false) genişletilmiş
            // izinlerle (audio/document/video) doğrulanır.
            var isOwnerOrProfileImageUpload = updateProfileImage || ownerType != ImageOwnerType.User;
            var validation = isOwnerOrProfileImageUpload
                ? UploadFileValidator.ValidateProfileOrOwnerImage(file)
                : UploadFileValidator.ValidateChatMedia(file);
            if (!validation.Success)
                return new ErrorDataResult<string>(validation.Message);

            // Sanitize edilmiş dosya adı (extension dahil) — blob'a yazılırken bu kullanılır.
            var sanitizedName = UploadFileValidator.GetSanitizedFileName(validation, fallback: file.FileName ?? "file");

            byte[] fileBytes;
            using (var ms = new System.IO.MemoryStream())
            {
                await file.CopyToAsync(ms);
                fileBytes = ms.ToArray();
            }
            var fileContentType = file.ContentType;
            var fileFileName = sanitizedName;

            if (!ShouldSkipImageModeration(fileContentType, fileFileName))
            {
                // İçerik denetimi — blob yüklemesinden ÖNCE, senkron olarak kontrol et.
                // Yasak görsel hiç depolanmaz.
                var moderationCheck = await _contentModerationService.CheckImageContentAsync(fileBytes, fileContentType, fileFileName);
                if (!moderationCheck.Success)
                    return new ErrorDataResult<string>(moderationCheck.Message);
            }

            var containerName = ownerType switch
            {
                ImageOwnerType.User => "user-images",
                ImageOwnerType.Store => "store-images",
                ImageOwnerType.FreeBarber => "freebarber-images",
                ImageOwnerType.ManuelBarber => "manuelbarber-images",
                _ => throw new ArgumentOutOfRangeException(nameof(ownerType), ownerType, "Geçersiz resim sahibi tipi")
            };

            // B4: Blob adı `{guid}{ext}` formatında — sanitize edilmiş extension kullanılır.
            // Bu sayede istemci "photo.exe" göndermiş olsa bile (validator zaten reddederdi
            // ama yine de güvenlik katmanı), blob'a tehlikeli uzantı yazılamaz.
            var blobExt = System.IO.Path.GetExtension(sanitizedName);
            var safeBlobName = $"{Guid.NewGuid()}{blobExt}";
            var imageUrl = await _blobStorageService.UploadAsync(file, containerName, safeBlobName);

            var urlWithTimestamp = $"{imageUrl}?t={DateTime.UtcNow.Ticks}";

            var image = new Image
            {
                Id = Guid.NewGuid(),
                ImageUrl = urlWithTimestamp,
                OwnerType = ownerType,
                ImageOwnerId = ownerId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _imageDal.Add(image);

            if (updateProfileImage && ownerType == ImageOwnerType.User)
            {
                var user = await _userDal.Get(u => u.Id == ownerId);
                if (user != null)
                {
                    user.ImageId = image.Id;
                    await _userDal.Update(user);
                }

                try
                {
                    await _realTimePublisher.PushImageUpdatedAsync(ownerId, image.Id, urlWithTimestamp);
                }
                catch
                {
                }
            }

            return new SuccessDataResult<string>(image.Id.ToString(), "Resim başarıyla yüklendi.");
        }

        [LogAspect(logParameters: true, logReturnValue: true)]
        public async Task<IDataResult<List<string>>> UploadImagesAsync(List<IFormFile> files, ImageOwnerType ownerType, Guid ownerId, Guid currentUserId)
        {
            var uploadAuth = await EnsureCurrentUserCanUploadAsync(ownerType, ownerId, currentUserId);
            if (!uploadAuth.Success)
                return new ErrorDataResult<List<string>>(uploadAuth.Message);

            if (files.Count > 3)
            {
                return new ErrorDataResult<List<string>>(
                    $"Tek seferde en fazla 3 resim yüklenebilir. Gönderilen: {files.Count}");
            }

            // B4: Çoklu yükleme her zaman galeri/profil resmi içindir (chat değil).
            // Her dosyayı sırayla validate et — bir tanesi bile fail ederse hiçbiri yüklenmez.
            var sanitizedNames = new List<string>(files.Count);
            for (int i = 0; i < files.Count; i++)
            {
                var v = UploadFileValidator.ValidateProfileOrOwnerImage(files[i]);
                if (!v.Success)
                    return new ErrorDataResult<List<string>>(
                        files.Count == 1 ? v.Message : $"Dosya #{i + 1}: {v.Message}");
                sanitizedNames.Add(UploadFileValidator.GetSanitizedFileName(v, fallback: files[i].FileName ?? "file"));
            }

            if (ownerId != Guid.Empty &&
                (ownerType == ImageOwnerType.Store ||
                 ownerType == ImageOwnerType.FreeBarber))
            {
                var maxImages = ownerType switch
                {
                    ImageOwnerType.Store => 3,
                    ImageOwnerType.FreeBarber => 3,
                    _ => 1
                };

                var allImagesCount = await _imageDal.CountAsync(x =>
                    x.ImageOwnerId == ownerId &&
                    x.OwnerType == ownerType);

                int galleryImagesCount = allImagesCount;

                if (ownerType == ImageOwnerType.Store)
                {
                    var store = await _barberStoreDal.Get(s => s.Id == ownerId);
                    if (store != null && store.TaxDocumentImageId.HasValue)
                    {
                        galleryImagesCount = await _imageDal.CountAsync(x =>
                            x.ImageOwnerId == ownerId &&
                            x.OwnerType == ownerType &&
                            x.Id != store.TaxDocumentImageId.Value);
                    }
                }
                else if (ownerType == ImageOwnerType.FreeBarber)
                {
                    var freeBarber = await _freeBarberDal.Get(fb => fb.Id == ownerId);
                    if (freeBarber != null && freeBarber.BarberCertificateImageId.HasValue)
                    {
                        galleryImagesCount = await _imageDal.CountAsync(x =>
                            x.ImageOwnerId == ownerId &&
                            x.OwnerType == ownerType &&
                            x.Id != freeBarber.BarberCertificateImageId.Value);
                    }
                }

                var totalCount = galleryImagesCount + files.Count;

                if (totalCount > maxImages)
                {
                    var ownerTypeText = ownerType switch
                    {
                        ImageOwnerType.Store => "Dükkan",
                        ImageOwnerType.FreeBarber => "Serbest berber",
                        _ => "Sahip"
                    };

                    return new ErrorDataResult<List<string>>(
                        $"{ownerTypeText} için en fazla {maxImages} galeri resmi eklenebilir. Mevcut galeri resim sayısı: {galleryImagesCount}, eklenmek istenen: {files.Count}, toplam: {totalCount}");
                }
            }

            var fileBytesMap = new List<(byte[] bytes, string contentType, string fileName)>();
            foreach (var f in files)
            {
                using var ms = new System.IO.MemoryStream();
                await f.CopyToAsync(ms);
                fileBytesMap.Add((ms.ToArray(), f.ContentType, f.FileName));
            }

            // İçerik denetimi — her resim için blob yüklemesinden ÖNCE, senkron kontrol.
            for (int i = 0; i < fileBytesMap.Count; i++)
            {
                var (bytes, ct, fn) = fileBytesMap[i];
                if (ShouldSkipImageModeration(ct, fn))
                    continue;
                var check = await _contentModerationService.CheckImageContentAsync(bytes, ct, fn);
                if (!check.Success)
                    return new ErrorDataResult<List<string>>(check.Message);
            }

            var containerName = ownerType switch
            {
                ImageOwnerType.User => "user-images",
                ImageOwnerType.Store => "store-images",
                ImageOwnerType.FreeBarber => "freebarber-images",
                ImageOwnerType.ManuelBarber => "manuelbarber-images",
                _ => throw new ArgumentOutOfRangeException(nameof(ownerType), ownerType, "Geçersiz resim sahibi tipi")
            };

            // B4: Sanitize edilmiş dosya adlarıyla teker teker yükle (UploadMultipleAsync
            // extension'ı kontrol etmiyordu). Bu sayede blob adı `{guid}{safeExt}` olur.
            var imageUrls = new List<string>(files.Count);
            for (int i = 0; i < files.Count; i++)
            {
                var blobExt = System.IO.Path.GetExtension(sanitizedNames[i]);
                var safeBlobName = $"{Guid.NewGuid()}{blobExt}";
                var url = await _blobStorageService.UploadAsync(files[i], containerName, safeBlobName);
                imageUrls.Add(url);
            }

            var images = imageUrls.Select(url => new Image
            {
                Id = Guid.NewGuid(),
                ImageUrl = url,
                OwnerType = ownerType,
                ImageOwnerId = ownerId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }).ToList();

            await _imageDal.AddRange(images);

            return new SuccessDataResult<List<string>>(imageUrls, $"{files.Count} resim başarıyla yüklendi.");
        }

        private async Task ModerateAndRemoveImageIfFlaggedAsync(
            Guid imageId, Guid ownerId, ImageOwnerType ownerType,
            string blobUrl, byte[] fileBytes, string contentType, string fileName, bool clearUserProfileIfFlagged)
        {
            try
            {
                var moderationResult = await _contentModerationService.CheckImageContentAsync(fileBytes, contentType, fileName);
                if (moderationResult.Success) return;

                if (!string.IsNullOrEmpty(blobUrl))
                {
                    try { await _blobStorageService.DeleteAsync(blobUrl); } catch { }
                }

                var entity = await _imageDal.Get(i => i.Id == imageId);
                if (entity != null)
                    await _imageDal.Remove(entity);

                if (clearUserProfileIfFlagged)
                {
                    var user = await _userDal.Get(u => u.Id == ownerId);
                    if (user != null && user.ImageId == imageId)
                    {
                        user.ImageId = null;
                        await _userDal.Update(user);
                    }
                }

                await _realTimePublisher.PushImageRemovedAsync(ownerId, imageId);
            }
            catch
            {
            }
        }

        public async Task<IDataResult<List<ImageGetDto>>> GetImagesByOwnerAsync(Guid ownerId, ImageOwnerType ownerType)
        {
            var images = await _imageDal.GetAll(x =>
                x.ImageOwnerId == ownerId &&
                x.OwnerType == ownerType);

            var orderedImages = images.OrderByDescending(i => i.CreatedAt).ToList();

            var dtos = orderedImages.Adapt<List<ImageGetDto>>();

            return new SuccessDataResult<List<ImageGetDto>>(dtos);
        }

        [LogAspect]
        public async Task<IResult> UpdateImageBlobAsync(Guid imageId, IFormFile file, Guid currentUserId)
        {
            var entity = await _imageDal.Get(i => i.Id == imageId);
            if (entity == null)
                return new ErrorResult("Resim bulunamadı.");

            var auth = await EnsureCurrentUserCanMutateExistingImageAsync(entity, currentUserId);
            if (!auth.Success)
                return auth;

            if (string.IsNullOrEmpty(entity.ImageUrl))
                return new ErrorResult("Resim URL'i bulunamadı.");

            // B4: Mevcut Image kaydının içeriği güncelleniyor; her zaman resim olmalı.
            var validation = UploadFileValidator.ValidateProfileOrOwnerImage(file);
            if (!validation.Success)
                return new ErrorResult(validation.Message);
            var sanitizedName = UploadFileValidator.GetSanitizedFileName(validation, fallback: file.FileName ?? "file");

            byte[] fileBytes;
            using (var ms = new System.IO.MemoryStream())
            {
                await file.CopyToAsync(ms);
                fileBytes = ms.ToArray();
            }
            var fileContentType = file.ContentType;
            var fileFileName = sanitizedName;

            if (!ShouldSkipImageModeration(fileContentType, fileFileName))
            {
                var moderationCheck = await _contentModerationService.CheckImageContentAsync(fileBytes, fileContentType, fileFileName);
                if (!moderationCheck.Success)
                    return new ErrorResult(moderationCheck.Message);
            }

            var updatedUrl = await _blobStorageService.UpdateAsync(file, entity.ImageUrl);

            var urlWithTimestamp = $"{updatedUrl}?t={DateTime.UtcNow.Ticks}";
            entity.ImageUrl = urlWithTimestamp;

            entity.UpdatedAt = DateTime.UtcNow;
            await _imageDal.Update(entity);

            if (entity.OwnerType == ImageOwnerType.User && entity.ImageOwnerId != Guid.Empty)
            {
                try
                {
                    await _realTimePublisher.PushImageUpdatedAsync(entity.ImageOwnerId, imageId, urlWithTimestamp);
                }
                catch
                {
                }
            }

            return new SuccessResult("Resim başarıyla güncellendi.");
        }

        private async Task<IResult> EnsureCurrentUserCanUploadAsync(ImageOwnerType ownerType, Guid ownerId, Guid currentUserId)
        {
            switch (ownerType)
            {
                case ImageOwnerType.User:
                    return ownerId == currentUserId
                        ? new SuccessResult()
                        : new ErrorResult(Messages.UnauthorizedOperation);
                case ImageOwnerType.Store:
                    var store = await _barberStoreDal.Get(s => s.Id == ownerId);
                    if (store == null)
                        return new ErrorResult(Messages.StoreNotFound);
                    return store.BarberStoreOwnerId == currentUserId
                        ? new SuccessResult()
                        : new ErrorResult(Messages.UnauthorizedOperation);
                case ImageOwnerType.FreeBarber:
                    var fb = await _freeBarberDal.Get(f => f.Id == ownerId);
                    if (fb == null)
                        return new ErrorResult(Messages.FreeBarberNotFound);
                    return fb.FreeBarberUserId == currentUserId
                        ? new SuccessResult()
                        : new ErrorResult(Messages.UnauthorizedOperation);
                case ImageOwnerType.ManuelBarber:
                    var mb = await _manuelBarberDal.Get(m => m.Id == ownerId);
                    if (mb == null)
                        return new ErrorResult(Messages.ManuelBarberNotFound);
                    var mbStore = await _barberStoreDal.Get(s => s.Id == mb.StoreId);
                    if (mbStore == null)
                        return new ErrorResult(Messages.StoreNotFound);
                    return mbStore.BarberStoreOwnerId == currentUserId
                        ? new SuccessResult()
                        : new ErrorResult(Messages.UnauthorizedOperation);
                default:
                    return new ErrorResult(Messages.UnauthorizedOperation);
            }
        }

        private async Task<IResult> EnsureCurrentUserCanMutateExistingImageAsync(Image image, Guid currentUserId)
        {
            return await EnsureCurrentUserCanUploadAsync(image.OwnerType, image.ImageOwnerId, currentUserId);
        }
    }
}
