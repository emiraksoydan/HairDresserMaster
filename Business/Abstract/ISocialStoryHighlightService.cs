using Core.Utilities.Results;
using Entities.Concrete.Dto;

namespace Business.Abstract
{
    public interface ISocialStoryHighlightService
    {
        Task<IDataResult<List<SocialStoryHighlightDto>>> GetProfileHighlightsAsync(Guid userId, Guid profileId);
        Task<IDataResult<SocialStoryHighlightDetailDto>> GetHighlightDetailAsync(Guid userId, Guid highlightId);
        Task<IDataResult<Guid>> CreateHighlightAsync(Guid userId, CreateSocialStoryHighlightRequest request);
        Task<IResult> UpdateHighlightAsync(Guid userId, Guid highlightId, UpdateSocialStoryHighlightRequest request);
        Task<IResult> AddStoriesToHighlightAsync(Guid userId, Guid highlightId, AddSocialStoryHighlightItemsRequest request);
        Task<IResult> RemoveHighlightItemAsync(Guid userId, Guid highlightId, Guid itemId);
        Task<IResult> DeleteHighlightAsync(Guid userId, Guid highlightId);
    }
}
