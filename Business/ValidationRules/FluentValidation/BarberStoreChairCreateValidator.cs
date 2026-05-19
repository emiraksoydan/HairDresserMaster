using Business.Resources;
using Entities.Concrete.Dto;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation
{
    public class BarberStoreChairCreateValidator : AbstractValidator<BarberChairCreateDto>
    {
        public BarberStoreChairCreateValidator()
        {
            RuleFor(x => x.BarberId)
            .NotEmpty()
            .When(x => string.IsNullOrWhiteSpace(x.Name))
            .WithMessage(Messages.ValidationChairBerberIfEmptyName);

            RuleFor(x => x.Name)
                .Must(name => string.IsNullOrWhiteSpace(name))
                .When(x => x.BarberId != null && x.BarberId != string.Empty)
                .WithMessage(Messages.ValidationChairNameEmptyWhenBarber);

            RuleFor(x => x)
                .Must(x =>
                {
                    var hasName = !string.IsNullOrWhiteSpace(x.Name);
                    var hasBarber = x.BarberId != null && x.BarberId != string.Empty;
                    return hasName ^ hasBarber;
                })
                .WithMessage(Messages.ValidationChairNameOrBarberRule);
        }
    }
}
