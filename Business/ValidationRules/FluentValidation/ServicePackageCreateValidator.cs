using Entities.Concrete.Dto;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation
{
    public class ServicePackageCreateValidator : AbstractValidator<ServicePackageCreateDto>
    {
        public ServicePackageCreateValidator()
        {
            RuleFor(x => x.OwnerId)
                .NotEmpty().WithMessage("Paket sahibi belirtilmelidir.");

            RuleFor(x => x.PackageName)
                .NotEmpty().WithMessage("Paket adı zorunludur.")
                .MaximumLength(100).WithMessage("Paket adı en fazla 100 karakter olabilir.");

            RuleFor(x => x.TotalPrice)
                .GreaterThan(0).WithMessage("Paket fiyatı 0'dan büyük olmalıdır.");

            RuleFor(x => x.ServiceOfferingIds)
                .NotNull().WithMessage("En az bir hizmet seçilmelidir.")
                .Must(ids => ids != null && ids.Count >= 1)
                .WithMessage("Paket oluşturmak için en az 1 hizmet seçilmelidir.");
        }
    }
}
