using Core.Utilities.Results;
using Entities.Concrete.Dto;

namespace Business.Abstract
{
    public interface IAdminAIAssistantService
    {
        Task<IDataResult<AdminAIChatResponseDto>> ChatAsync(Guid adminId, AdminAIChatRequestDto request);
        Task<IDataResult<AdminAIChatResponseDto>> ConfirmActionsAsync(Guid adminId, AdminAIConfirmRequestDto request);
    }
}
