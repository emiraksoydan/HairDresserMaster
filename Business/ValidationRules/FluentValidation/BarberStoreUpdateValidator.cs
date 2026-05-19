using Business.Resources;
using Core.Utilities.Constants;
using Entities.Concrete.Dto;
using Entities.Concrete.Enums;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.ValidationRules.FluentValidation
{
    public class BarberStoreUpdateDtoValidator : AbstractValidator<BarberStoreUpdateDto>
    {
        public BarberStoreUpdateDtoValidator()
        {
            RuleFor(x => x.StoreName)
                .NotEmpty().WithMessage(Messages.ValidationStoreNameRequired)
                .MinimumLength(2).WithMessage(Messages.ValidationStoreNameMin2)
                .MaximumLength(100).WithMessage(Messages.ValidationStoreNameMax100);

            RuleFor(x => x.Type)
                .IsInEnum().WithMessage(Messages.ValidationBusinessTypeInvalidWithPeriod);

            RuleFor(x => x.PricingType)
                .IsInEnum().WithMessage(Messages.ValidationPricingServiceTypeInvalid);

            RuleFor(x => x.AddressDescription)
                .NotEmpty().WithMessage(Messages.ValidationAddressDescriptionRequired);

            RuleFor(x => x.Latitude)
                .InclusiveBetween(-90, 90).WithMessage(Messages.ValidationLatRangeGeneric);

            RuleFor(x => x.Longitude)
                .InclusiveBetween(-180, 180).WithMessage(Messages.ValidationLonRangeGeneric);

            RuleFor(x => x.TaxDocumentImageId)
                .NotNull().WithMessage(Messages.ValidationTaxDocumentRequired)
                .NotEmpty().WithMessage(Messages.ValidationTaxDocumentRequired);

            // PricingValue koşullu
            When(x => x.PricingType == PricingType.Rent, () =>
            {
                RuleFor(x => x.PricingValue)
                    .NotNull().WithMessage(Messages.ValidationPriceRequired)
                    .GreaterThanOrEqualTo(0).WithMessage(Messages.ValidationStorePriceNonNegativeUpdate)
                    .Must(v => (decimal)v <= PriceLimits.MaxMonetaryTry).WithMessage(PriceLimits.MaxMonetaryTryMessage);
            });

            When(x => x.PricingType == PricingType.Percent, () =>
            {
                RuleFor(x => x.PricingValue)
                    .NotNull().WithMessage(Messages.ValidationPercentRequired)
                    .GreaterThan(0).WithMessage(Messages.ValidationPercentPositive)
                    .LessThanOrEqualTo(100).WithMessage(Messages.ValidationPercentMax100);
            });

            // Chairs
            RuleFor(x => x.Chairs)
                 .NotNull()
                 .NotEmpty()
                 .WithMessage(Messages.ValidationAtLeastOneChair);

            RuleForEach(x => x.Chairs).ChildRules(ch =>
            {
                ch.RuleFor(c => c.Name)
                  .NotEmpty()
                  .When(c => c.BarberId == null)
                  .WithMessage(Messages.ValidationChairNameWhenNoBarber);
            });

            // ManuelBarbers
            RuleForEach(x => x.ManuelBarbers).ChildRules(b =>
            {
                b.RuleFor(m => m.FullName)
                 .NotEmpty()
                 .WithMessage(Messages.ValidationManuelBarberNameRequired);
            });

            // Berberlerin toplamı 30'u geçmemeli
            RuleFor(x => x.ManuelBarbers)
                .Must(barbers => (barbers?.Count ?? 0) <= 30)
                .WithMessage(Messages.ValidationManuelBarberCountMax30);

            // Koltukların toplamı 30'u geçmemeli
            RuleFor(x => x.Chairs)
                .Must(chairs => (chairs?.Count ?? 0) <= 30)
                .WithMessage(Messages.ValidationChairCountMax30)
                .When(x => x.Chairs != null);

            // Offerings
            RuleFor(x => x.Offerings)
                .NotEmpty().WithMessage(Messages.ValidationAtLeastOneServiceOffering);

            RuleForEach(x => x.Offerings).ChildRules(o =>
            {
                o.RuleFor(v => v.ServiceName)
                 .NotEmpty().WithMessage(Messages.ValidationServiceNameNotEmpty);

                o.RuleFor(v => v.Price)
                 .NotNull().WithMessage(Messages.ValidationServicePriceRequired)
                 .GreaterThanOrEqualTo(0).WithMessage(Messages.ValidationServicePriceNonNegative)
                 .LessThanOrEqualTo(PriceLimits.MaxMonetaryTry).WithMessage(PriceLimits.MaxMonetaryTryMessage);
            });

            // Hizmet adları benzersiz (case-insensitive)
            RuleFor(x => x.Offerings)
                .Must(list => list.Select(i => i.ServiceName?.Trim().ToLowerInvariant())
                                  .Where(s => !string.IsNullOrWhiteSpace(s))
                                  .GroupBy(s => s!)
                                  .All(g => g.Count() == 1))
                .WithMessage(Messages.ValidationServiceNamesUnique);

            // Working hours
            RuleFor(x => x.WorkingHours)
                .NotNull().WithMessage(Messages.ValidationWorkingHoursRequired)
                .Must(w => w.Count > 0).WithMessage(Messages.ValidationAtLeastOneWorkingDay);

            // Aynı güne iki kayıt olmasın
            RuleFor(x => x.WorkingHours!)
                .Must(list =>
                {
                    var groups = list.GroupBy(i => i.DayOfWeek);
                    return groups.All(g => g.Count() == 1);
                })
                .WithMessage(Messages.ValidationOneWorkingEntryPerDay);

            // Saat detay kuralları (kapalı olmayan günlerde)
            RuleForEach(x => x.WorkingHours!)
                .Where(w => !w.IsClosed)
                .ChildRules(c =>
                {
                    c.RuleFor(w => w.StartTime)
                        .NotEmpty().WithMessage(Messages.ValidationStartTimeRequired)
                        .Must(IsHHmm).WithMessage(Messages.ValidationStartTimeHHmm);

                    c.RuleFor(w => w.EndTime)
                        .NotEmpty().WithMessage(Messages.ValidationEndTimeRequired)
                        .Must(IsHHmm).WithMessage(Messages.ValidationEndTimeHHmm);

                    c.RuleFor(w => w)
                        .Must(w => TryParseHHmm(w.StartTime, out var s) &&
                                   TryParseHHmm(w.EndTime, out var e) &&
                                   s < e)
                        .WithMessage(Messages.ValidationStartBeforeEndTime)
                        .When(w => IsHHmm(w.StartTime) && IsHHmm(w.EndTime));

                    // 1 saatlik slot kontrolü ve minimum/maksimum saat kontrolleri kaldırıldı
                });



        }
        private static bool TryParseHHmm(string? s, out TimeSpan t)
        {
            t = default;

            if (string.IsNullOrWhiteSpace(s))
                return false;

            // "HH:mm" formatında DateTime olarak parse et
            if (!DateTime.TryParseExact(
                    s,
                    "HH:mm",                        // tam mesajda yazdığın format
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var dt))
            {
                return false;
            }

            t = dt.TimeOfDay;  // 09:00 -> 09:00 TimeSpan
            return true;
        }
        private static bool IsHHmm(string? s) => TryParseHHmm(s, out _);
    }
}
