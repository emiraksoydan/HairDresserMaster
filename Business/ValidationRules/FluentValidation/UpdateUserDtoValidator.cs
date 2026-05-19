using Business.Resources;
using Entities.Concrete.Dto;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation
{
    public class UpdateUserDtoValidator : AbstractValidator<UpdateUserDto>
    {
        public UpdateUserDtoValidator()
        {
            RuleFor(x => x.FirstName)
                .NotEmpty().WithMessage(Messages.ValidationProfileFirstNameRequired)
                .MinimumLength(2).WithMessage(Messages.ValidationProfileFirstNameMin2)
                .MaximumLength(20).WithMessage(Messages.ValidationProfileFirstNameMax20);

            RuleFor(x => x.LastName)
                .NotEmpty().WithMessage(Messages.ValidationProfileLastNameRequired)
                .MinimumLength(2).WithMessage(Messages.ValidationProfileLastNameMin2)
                .MaximumLength(20).WithMessage(Messages.ValidationProfileLastNameMax20);

            RuleFor(x => x.PhoneNumber)
                     .NotNull().WithMessage(Messages.ValidationProfilePhoneRequired)
                     .NotEmpty().WithMessage(Messages.ValidationProfilePhoneNotEmpty)
                     .Matches(@"^\+90[0-9]{10}$").WithMessage(Messages.ValidationProfilePhoneE164Format);
        }
    }
}
