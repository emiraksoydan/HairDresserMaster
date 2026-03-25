using Business.Abstract;
using Business.Resources;
using Entities.Concrete.Dto;
using Entities.Concrete.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    public class ImageController : BaseApiController
    {
        private readonly IImageService _imageService;

        public ImageController(IImageService imageService)
        {
            _imageService = imageService;
        }

        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadImage([FromForm] ImageUploadRequest request, [FromQuery] bool? isProfileImage = null)
        {
            if (request.OwnerId == Guid.Empty)
                return BadRequest(Messages.ImageOwnerIdRequired);

            var updateProfileImage = isProfileImage ?? request.IsProfileImage;

            return await HandleDataResultAsync(
                _imageService.UploadImageAsync(
                    request.File,
                    request.OwnerType,
                    request.OwnerId,
                    CurrentUserId,
                    updateProfileImage));
        }

        [HttpPost("upload-multiple")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadImages([FromForm] ImageMultiUploadRequestDto request)
        {
            if (request.OwnerId == Guid.Empty)
                return BadRequest(Messages.ImageOwnerIdRequired);

            return await HandleDataResultAsync(
                _imageService.UploadImagesAsync(
                    request.Files,
                    request.OwnerType,
                    request.OwnerId,
                    CurrentUserId));
        }

        [HttpGet("owner/{ownerId}")]
        public async Task<IActionResult> GetImagesByOwner(
            Guid ownerId,
            [FromQuery] ImageOwnerType ownerType)
        {
            return await HandleDataResultAsync(
                _imageService.GetImagesByOwnerAsync(ownerId, ownerType));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteImage(Guid id)
        {
            return await HandleDeleteOperation(id, _imageService.DeleteAsync);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetImage(Guid id)
        {
            return await HandleDataResultAsync(_imageService.GetImage(id));
        }

        [HttpPut("update-blob")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpdateImageBlob(
            [FromForm] UpdateImageBlobRequestDto request)
        {
            if (request.ImageId == Guid.Empty)
                return BadRequest(Messages.ImageIdRequired);

            return await HandleResultAsync(
                _imageService.UpdateImageBlobAsync(
                    request.ImageId,
                    request.File,
                    CurrentUserId));
        }
    }
}
