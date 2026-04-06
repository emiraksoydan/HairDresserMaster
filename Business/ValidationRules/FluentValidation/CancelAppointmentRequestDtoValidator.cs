using Business.Resources;
using Entities.Concrete.Dto;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation
{
    public class CancelAppointmentRequestDtoValidator : AbstractValidator<CancelAppointmentRequestDto>
    {
        public CancelAppointmentRequestDtoValidator()
        {
            RuleFor(x => x.CancellationReason)
                .MaximumLength(CancelAppointmentRequestDto.CancellationReasonMaxLength)
                .WithMessage(Messages.AppointmentCancellationReasonTooLong);
        }
    }
}
