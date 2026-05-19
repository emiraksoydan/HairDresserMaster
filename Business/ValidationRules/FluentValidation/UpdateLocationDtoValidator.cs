using Business.Resources;
using Entities.Concrete.Dto;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation
{
    public class UpdateLocationDtoValidator : AbstractValidator<UpdateLocationDto>
    {
        public UpdateLocationDtoValidator()
        {
            // Id artık zorunlu değil - CurrentUserId kullanılıyor
            // RuleFor(x => x.Id) kaldırıldı

            // Latitude zorunlu ve geçerli aralıkta
            RuleFor(x => x.Latitude)
                .NotNull().WithMessage(Messages.ValidationLatitudeRequired)
                .InclusiveBetween(-90, 90).WithMessage(Messages.ValidationLatitudeRange);

            // Longitude zorunlu ve geçerli aralıkta
            RuleFor(x => x.Longitude)
                .NotNull().WithMessage(Messages.ValidationLongitudeRequired)
                .InclusiveBetween(-180, 180).WithMessage(Messages.ValidationLongitudeRange);
        }
    }
}
