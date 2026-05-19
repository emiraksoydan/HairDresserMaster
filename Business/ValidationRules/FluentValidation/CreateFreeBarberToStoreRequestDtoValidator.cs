using Business.Resources;
using Entities.Concrete.Dto;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation
{
    public class CreateFreeBarberToStoreRequestDtoValidator : AbstractValidator<CreateAppointmentRequestDto>
    {
        public CreateFreeBarberToStoreRequestDtoValidator()
        {
            // StartTime ve EndTime zorunlu
            RuleFor(x => x.StartTime)
                .NotNull().WithMessage(Messages.ValidationStartTimeRequired);

            RuleFor(x => x.EndTime)
                .NotNull().WithMessage(Messages.ValidationEndTimeRequired);

            // AppointmentDate zorunlu
            RuleFor(x => x.AppointmentDate)
                .NotNull().WithMessage(Messages.ValidationAppointmentDateRequired);

            // StoreId zorunlu
            RuleFor(x => x.StoreId)
                .NotEmpty().WithMessage(Messages.ValidationStoreSelectionRequired);

            // FreeBarberUserId gönderilmemeli (metod parametresinden geliyor)
            RuleFor(x => x.FreeBarberUserId)
                .Must(fbId => !fbId.HasValue)
                .WithMessage(Messages.ValidationFreeBarberIdNotInBody);

            // StoreSelectionType gönderilmemeli
            RuleFor(x => x.StoreSelectionType)
                .Must(st => !st.HasValue)
                .WithMessage(Messages.ValidationStoreSelectionTypeNotAllowedHere);
        }
    }
}
