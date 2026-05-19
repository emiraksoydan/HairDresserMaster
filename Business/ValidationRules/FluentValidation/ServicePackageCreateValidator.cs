using Business.Resources;
using Core.Utilities.Constants;
using Entities.Concrete.Dto;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation
{
    public class ServicePackageCreateValidator : AbstractValidator<ServicePackageCreateDto>
    {
        public ServicePackageCreateValidator()
        {
            RuleFor(x => x.OwnerId)
                .NotEmpty().WithMessage(Messages.ValidationPackageOwnerRequired);

            RuleFor(x => x.PackageName)
                .NotEmpty().WithMessage(Messages.ValidationPackageNameRequired)
                .MaximumLength(100).WithMessage(Messages.ValidationPackageNameMax100);

            RuleFor(x => x.TotalPrice)
                .GreaterThan(0).WithMessage(Messages.ValidationPackagePricePositive)
                .LessThanOrEqualTo(PriceLimits.MaxMonetaryTry).WithMessage(PriceLimits.MaxMonetaryTryMessage);

            RuleFor(x => x.ServiceOfferingIds)
                .NotNull().WithMessage(Messages.ValidationAtLeastOneServiceSelected)
                .Must(ids => ids != null && ids.Count >= 1)
                .WithMessage(Messages.ValidationPackageMinOneServiceCreate);
        }
    }
}
