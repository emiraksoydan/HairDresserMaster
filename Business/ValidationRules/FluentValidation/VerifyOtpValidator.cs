using Business.Resources;
using Entities.Concrete.Dto;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation
{
    public class VerifyOtpValidator : AbstractValidator<UserForVerifyDto>
    {
        public VerifyOtpValidator()
        {
            RuleFor(x => x.PhoneNumber)
                .NotEmpty()
                .WithMessage(Messages.ValidationPhoneRequired)
                .Matches(@"^\+90\d{10}$")
                .WithMessage(Messages.ValidationPhoneTurkeyE164);
            RuleFor(x => x.Code).NotEmpty().NotNull().WithMessage(Messages.ValidationOtpCodeRequired);
            When(x => x.Mode == "register", () =>
            {
                RuleFor(x => x.FirstName)
                    .NotEmpty().WithMessage(Messages.ValidationFirstNameRequiredRegister);

                RuleFor(x => x.LastName)
                    .NotEmpty().WithMessage(Messages.ValidationLastNameRequiredRegister);

            });
        }
    }
}
