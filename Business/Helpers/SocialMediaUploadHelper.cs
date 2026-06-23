using Core.Utilities.Results;
using Core.Utilities.Storage;
using Entities.Concrete.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Business.Helpers
{
    /// <summary>
    /// Sosyal medya medya yüklemeleri — dosya baytları bir kez okunur; blob'a bayt dizisi yazılır.
    /// </summary>
    public static class SocialMediaUploadHelper
    {
        public static async Task<IDataResult<string>> UploadAsync(
            IFormFile file,
            bool isVideo,
            string containerName,
            IBlobStorageService blobStorage,
            ILogger logger)
        {
            if (file == null || file.Length == 0)
                return new ErrorDataResult<string>(SocialErrorCodes.PostMediaRequired);

            var validation = isVideo
                ? UploadFileValidator.ValidateChatMedia(file)
                : UploadFileValidator.ValidateProfileOrOwnerImage(file);
            if (!validation.Success)
                return new ErrorDataResult<string>(validation.Message);

            byte[] fileBytes;
            await using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms);
                fileBytes = ms.ToArray();
            }

            if (fileBytes.Length == 0)
                return new ErrorDataResult<string>(SocialErrorCodes.MediaUploadFailed);

            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(ext))
                ext = isVideo ? ".mp4" : ".jpg";

            var safeBlobName = $"{Guid.NewGuid()}{ext}";
            string url;
            try
            {
                url = await blobStorage.UploadBytesAsync(fileBytes, containerName, safeBlobName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Social] Medya blob yüklemesi başarısız. Dosya: {FileName}", file.FileName);
                return new ErrorDataResult<string>(SocialErrorCodes.MediaUploadFailed);
            }

            if (string.IsNullOrWhiteSpace(url))
                return new ErrorDataResult<string>(SocialErrorCodes.MediaUploadFailed);

            // İki parametreli ctor: T=string iken tek parametre Message alanına yazılıyordu (Data null kalıyordu).
            return new SuccessDataResult<string>(url, string.Empty);
        }
    }
}
