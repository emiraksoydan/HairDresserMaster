using System;
using System.Collections.Generic;
using Entities.Abstract;

namespace Entities.Concrete.Dto
{
    /// <summary>
    /// Tek gün için koltuk/slot müsaitlik özeti (<see cref="ChairSlotDto"/> ile aynı şekil, çoklu gün batch için).
    /// </summary>
    public class StoreDayAvailabilityDto : IDto
    {
        public DateOnly Date { get; set; }
        public List<ChairSlotDto> Chairs { get; set; } = new();
    }
}
