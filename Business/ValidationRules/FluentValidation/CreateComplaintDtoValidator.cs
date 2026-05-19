using Business.Resources;
using Entities.Concrete.Dto;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation
{
    public class CreateComplaintDtoValidator : AbstractValidator<CreateComplaintDto>
    {
        public CreateComplaintDtoValidator()
        {
            RuleFor(x => x.ComplaintToUserId)
                .NotEmpty().WithMessage(Messages.ValidationComplaintTargetRequired);

            RuleFor(x => x.ComplaintReason)
                .MaximumLength(1000).WithMessage(Messages.ValidationComplaintReasonMax1000);
        }
    }
}
