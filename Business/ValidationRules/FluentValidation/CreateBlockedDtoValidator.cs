using Business.Resources;
using Entities.Concrete.Dto;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation
{
    public class CreateBlockedDtoValidator : AbstractValidator<CreateBlockedDto>
    {
        public CreateBlockedDtoValidator()
        {
            RuleFor(x => x.BlockedToUserId)
                .NotEmpty().WithMessage(Messages.ValidationBlockTargetRequired);

            RuleFor(x => x.BlockReason)
                .MaximumLength(500).WithMessage(Messages.ValidationBlockReasonMax500);
        }
    }
}
