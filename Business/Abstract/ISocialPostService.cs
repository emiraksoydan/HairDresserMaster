using Core.Utilities.Results;
using Entities.Concrete.Dto;
using Entities.Concrete.Enums;
using Microsoft.AspNetCore.Http;

namespace Business.Abstract
{
    public interface ISocialPostService
    {
        Task<IDataResult<Guid>> CreatePostAsync(
            Guid userId,
            Guid profileId,
            string? caption,
            SocialPostType type,
            IReadOnlyList<IFormFile> files,
            int? durationSec,
            IReadOnlyList<int>? durationSecs = null,
            Guid? appointmentId = null);

        Task<IDataResult<List<SocialPostDto>>> GetFeedAsync(
            Guid userId,
            DateTime? beforeUtc,
            Guid? beforeId,
            int limit = 20);

        Task<IDataResult<List<SocialPostDto>>> GetProfilePostsAsync(
            Guid userId,
            Guid profileId,
            SocialPostType? typeFilter,
            DateTime? beforeUtc,
            Guid? beforeId,
            int limit = 20);

        Task<IDataResult<SocialPostDto>> GetPostAsync(Guid userId, Guid postId);
        Task<IDataResult<SocialPostDto>> UpdatePostCaptionAsync(Guid userId, Guid postId, string? caption);
        Task<IResult> DeletePostAsync(Guid userId, Guid postId);
        Task<IResult> RecordViewAsync(Guid userId, Guid profileId, Guid postId);
        Task<IDataResult<List<SocialPostDto>>> GetReelsFeedAsync(
            Guid userId,
            DateTime? beforeUtc,
            Guid? beforeId,
            int limit = 20,
            double? latitude = null,
            double? longitude = null,
            double radiusKm = 50);

        Task<IDataResult<List<SocialPostDto>>> GetDiscoverPostsAsync(
            Guid userId,
            string? query,
            double? latitude,
            double? longitude,
            double radiusKm,
            Guid? profileId,
            DateTime? beforeUtc,
            Guid? beforeId,
            int limit = 30,
            AvailabilityFilter? availability = null,
            IReadOnlyList<Guid>? serviceIds = null);

        Task<IDataResult<List<SocialPostDto>>> GetSavedPostsAsync(
            Guid userId,
            Guid profileId,
            SocialPostType? typeFilter,
            SocialPostType? excludeType,
            DateTime? beforeUtc,
            Guid? beforeId,
            int limit = 30);

        Task<IResult> PinPostAsync(Guid userId, Guid postId);
        Task<IResult> UnpinPostAsync(Guid userId, Guid postId);
    }
}
