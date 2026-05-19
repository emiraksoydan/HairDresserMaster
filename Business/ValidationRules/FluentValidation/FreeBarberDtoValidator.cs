using Business.Resources;
using Core.Utilities.Constants;
using Entities.Concrete.Dto;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation
{
    public class FreeBarberDtoValidator : AbstractValidator<FreeBarberCreateDto>
    {
        public FreeBarberDtoValidator()
        {
            RuleFor(x => x.FirstName)
                .NotEmpty().WithMessage(Messages.ValidationFirstNameRequired)
                .MinimumLength(2).WithMessage(Messages.ValidationFirstNameMin2)
                .MaximumLength(50).WithMessage(Messages.ValidationFirstNameMax50);
            RuleFor(x => x.LastName)
                .NotEmpty().WithMessage(Messages.ValidationLastNameRequired)
                .MinimumLength(2).WithMessage(Messages.ValidationLastNameMin2)
                .MaximumLength(50).WithMessage(Messages.ValidationLastNameMax50);
            RuleFor(x => x.Type).NotNull().WithMessage(Messages.ValidationBusinessTypeRequired).IsInEnum().WithMessage(Messages.ValidationBusinessTypeInvalid);
            RuleFor(x => x.Offerings).NotNull().WithMessage(Messages.ValidationServiceListRequired).Must(x => x.Count > 0).WithMessage(Messages.ValidationAtLeastOneServiceOffering);
            RuleForEach(x => x.Offerings).ChildRules(o =>
            {
                o.RuleFor(x => x.ServiceName)
                    .NotEmpty().WithMessage(Messages.ValidationServiceNameNotEmpty);

                o.RuleFor(x => x.Price)
                    .NotNull().WithMessage(Messages.ValidationServicePriceRequired)
                    .GreaterThanOrEqualTo(0).WithMessage(Messages.ValidationServicePriceNonNegative)
                    .LessThanOrEqualTo(PriceLimits.MaxMonetaryTry).WithMessage(PriceLimits.MaxMonetaryTryMessage);
            });
            RuleFor(x => x.Latitude)
                .InclusiveBetween(-90, 90).WithMessage(Messages.ValidationLatRangeGeneric);

            RuleFor(x => x.Longitude)
                .InclusiveBetween(-180, 180).WithMessage(Messages.ValidationLonRangeGeneric);

            // BarberCertificateImageId artık opsiyonel (güzellik salonu belgesi için)

        }
    }
}
