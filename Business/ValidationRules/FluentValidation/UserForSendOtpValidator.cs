using Entities.Concrete.Dto;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation
{
    /// <summary>+90 + 10 haneli ulusal cep (E.164 toplam 13 karakter: +905XXXXXXXXX).</summary>
    public class UserForSendOtpValidator : AbstractValidator<UserForSendOtpDto>
    {
        public UserForSendOtpValidator()
        {
            RuleFor(x => x.PhoneNumber)
                .NotEmpty()
                .WithMessage("Telefon numarası zorunludur.")
                .Matches(@"^\+90\d{10}$")
                .WithMessage("Geçerli bir Türkiye cep numarası girin (+90 ile başlayan 10 hane, örn. +905551234567).");

            RuleFor(x => x.Language)
                .MaximumLength(10)
                .When(x => !string.IsNullOrEmpty(x.Language))
                .WithMessage("Geçersiz dil kodu.");
        }
    }
}
