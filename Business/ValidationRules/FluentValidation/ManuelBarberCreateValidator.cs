using Business.Resources;
using Entities.Concrete.Dto;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation
{
    public class ManuelBarberCreateValidator : AbstractValidator<ManuelBarberCreateDto>
    {
        public ManuelBarberCreateValidator()
        {
            RuleFor(x => x.FullName).NotEmpty().WithMessage(Messages.ValidationManuelBarberFullNameRequired);
            RuleFor(x => x.StoreId).NotEmpty().WithMessage(Messages.ValidationStoreIdRequired);
        }
    }
}
