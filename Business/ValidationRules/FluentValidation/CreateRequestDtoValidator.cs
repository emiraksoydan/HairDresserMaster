using Business.Resources;
using Entities.Concrete.Dto;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation
{
    public class CreateRequestDtoValidator : AbstractValidator<CreateRequestDto>
    {
        public CreateRequestDtoValidator()
        {
            RuleFor(x => x.RequestTitle)
                .NotEmpty().WithMessage(Messages.ValidationRequestTitleNotEmpty)
                .MaximumLength(200).WithMessage(Messages.ValidationRequestTitleMax200);

            RuleFor(x => x.RequestMessage)
                .NotEmpty().WithMessage(Messages.ValidationRequestMessageNotEmpty)
                .MaximumLength(2000).WithMessage(Messages.ValidationRequestMessageMax2000);
        }
    }
}
