using Business.Resources;
using Entities.Concrete.Dto;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation
{
    public class CreateStoreToFreeBarberRequestDtoValidator : AbstractValidator<CreateStoreToFreeBarberRequestDto>
    {
        public CreateStoreToFreeBarberRequestDtoValidator()
        {
            RuleFor(x => x.StoreId)
                .NotEmpty().WithMessage(Messages.ValidationStoreSelectionRequired);

            RuleFor(x => x.FreeBarberUserId)
                .NotEmpty().WithMessage(Messages.ValidationFreeBarberSelectionRequired);
        }
    }
}
