using Business.Resources;
using Entities.Concrete.Dto;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation
{
    public class AddStoreToAppointmentRequestDtoValidator : AbstractValidator<AddStoreToAppointmentRequestDto>
    {
        public AddStoreToAppointmentRequestDtoValidator()
        {
            // StoreId zorunlu
            RuleFor(x => x.StoreId)
                .NotEmpty().WithMessage(Messages.ValidationStoreSelectionRequired);

            // ChairId zorunlu
            RuleFor(x => x.ChairId)
                .NotEmpty().WithMessage(Messages.ValidationChairSelectionRequired);

            // AppointmentDate zorunlu
            RuleFor(x => x.AppointmentDate)
                .NotNull().WithMessage(Messages.ValidationAppointmentDateRequired);

            // StartTime ve EndTime zorunlu
            RuleFor(x => x.StartTime)
                .NotNull().WithMessage(Messages.ValidationStartTimeRequired);

            RuleFor(x => x.EndTime)
                .NotNull().WithMessage(Messages.ValidationEndTimeRequired);

            // ServiceOfferingIds zorunlu (en az 1)
            RuleFor(x => x.ServiceOfferingIds)
                .NotEmpty().WithMessage(Messages.ValidationServiceSelectionRequired)
                .Must(ids => ids != null && ids.Count > 0).WithMessage(Messages.ValidationAtLeastOneServiceSelected);
        }
    }
}
