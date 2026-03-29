using System;
using System.Collections.Generic;
using System.Linq;
using Entities.Concrete.Dto;

namespace Business.Helpers
{
    /// <summary>
    /// Birden fazla mağazanın <see cref="EarningsDto"/> sonuçlarını tek raporda birleştirir (shop-insights çoklu seçim).
    /// </summary>
    public static class EarningsMergeHelper
    {
        public static EarningsDto Merge(IReadOnlyList<EarningsDto> parts)
        {
            if (parts == null || parts.Count == 0)
                return new EarningsDto();

            if (parts.Count == 1)
                return parts[0];

            decimal total = 0;
            decimal daily = 0;
            decimal prev = 0;
            var byDay = new Dictionary<string, decimal>();

            foreach (var e in parts)
            {
                total += e.TotalEarnings;
                daily += e.DailyEarnings;
                prev += e.PreviousPeriodEarnings;

                if (e.DailyBreakdown == null) continue;

                foreach (var b in e.DailyBreakdown)
                {
                    var key = b.Date ?? "";
                    if (!byDay.ContainsKey(key))
                        byDay[key] = 0;
                    byDay[key] += b.Amount;
                }
            }

            var changePct = prev == 0
                ? (total > 0 ? 100.0 : 0.0)
                : (double)((total - prev) / prev * 100);

            var breakdown = byDay
                .OrderBy(x => x.Key)
                .Select(x => new DailyEarningDto { Date = x.Key, Amount = x.Value })
                .ToList();

            return new EarningsDto
            {
                TotalEarnings = total,
                DailyEarnings = daily,
                PreviousPeriodEarnings = prev,
                ChangePercent = Math.Round(changePct, 1),
                DailyBreakdown = breakdown
            };
        }
    }
}
