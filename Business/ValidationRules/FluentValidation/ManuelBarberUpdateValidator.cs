using Business.Resources;
using Entities.Concrete.Dto;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation
{

    public class ManuelBarberUpdateValidator : AbstractValidator<ManuelBarberUpdateDto>
    {
        public ManuelBarberUpdateValidator()
        {
            RuleFor(x => x.FullName).NotEmpty().WithMessage(Messages.ValidationManuelBarberFullNameRequired);
        }
    }
}
