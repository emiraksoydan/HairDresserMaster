using Core.Utilities.Results;
using Entities.Concrete.Dto;
using Entities.Concrete.Enums;
using Microsoft.AspNetCore.Http;

namespace Business.Abstract
{
    public interface IImageService
    {
        Task<IResult> DeleteAsync(Guid id, Guid currentUserId);

        Task<IDataResult<ImageGetDto>> GetImage(Guid id);

        Task<IDataResult<string>> UploadImageAsync(IFormFile file, ImageOwnerType ownerType, Guid ownerId, Guid currentUserId, bool updateProfileImage = true);

        Task<IDataResult<List<string>>> UploadImagesAsync(List<IFormFile> files, ImageOwnerType ownerType, Guid ownerId, Guid currentUserId);

        Task<IDataResult<List<ImageGetDto>>> GetImagesByOwnerAsync(Guid ownerId, ImageOwnerType ownerType);

        Task<IResult> UpdateImageBlobAsync(Guid imageId, IFormFile file, Guid currentUserId);
    }
}
