using Business.Resources;
using Entities.Concrete.Dto;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation
{
    public class CreateCustomerToStoreRequestDtoValidator : AbstractValidator<CreateAppointmentRequestDto>
    {
        public CreateCustomerToStoreRequestDtoValidator()
        {
            // FreeBarberUserId gönderilmemeli
            RuleFor(x => x.FreeBarberUserId)
                .Must(fbId => !fbId.HasValue)
                .WithMessage(Messages.ValidationStoreAppointmentNoFreeBarber);

            // ChairId zorunlu
            RuleFor(x => x.ChairId)
                .NotEmpty().WithMessage(Messages.ValidationChairSelectionRequired);

            // StartTime ve EndTime zorunlu
            RuleFor(x => x.StartTime)
                .NotNull().WithMessage(Messages.ValidationStartTimeRequired);

            RuleFor(x => x.EndTime)
                .NotNull().WithMessage(Messages.ValidationEndTimeRequired);

            // AppointmentDate zorunlu
            RuleFor(x => x.AppointmentDate)
                .NotNull().WithMessage(Messages.ValidationAppointmentDateRequired);

            // Konum zorunlu
            RuleFor(x => x.RequestLatitude)
                .NotNull().WithMessage(Messages.ValidationLocationLatitudeRequired);

            RuleFor(x => x.RequestLongitude)
                .NotNull().WithMessage(Messages.ValidationLocationLongitudeRequired);

            // StoreId zorunlu (implicit - ChairId'den kontrol edilebilir ama açıkça belirtelim)
            RuleFor(x => x.StoreId)
                .NotEmpty().WithMessage(Messages.ValidationStoreSelectionRequired);
        }
    }
}
