using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Business.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation
{
    public class VerifyOtpValidator : AbstractValidator<UserForVerifyDto>
    {
        public VerifyOtpValidator()
        {
            RuleFor(x => x.PhoneNumber)
                .NotEmpty()
                .WithMessage("Telefon numarası zorunludur.")
                .Matches(@"^\+90\d{10}$")
                .WithMessage("Geçerli bir Türkiye cep numarası girin (+90 ile başlayan 10 hane, örn. +905551234567).");
            RuleFor(x => x.Code).NotEmpty().NotNull().WithMessage("Kod girilmelidir");
            When(x => x.Mode == "register", () =>
            {
                RuleFor(x => x.FirstName)
                    .NotEmpty().WithMessage("İsim gerekli");

                RuleFor(x => x.LastName)
                    .NotEmpty().WithMessage("Soyisim gerekli");

            });
        }
    }
}
