namespace Entities.Concrete.Dto
{
    public class EarningsDto
    {
        /// <summary>Seçilen dönemdeki toplam kazanç</summary>
        public decimal TotalEarnings { get; set; }

        /// <summary>Bugünkü kazanç</summary>
        public decimal DailyEarnings { get; set; }

        /// <summary>Önceki dönemdeki toplam kazanç (karşılaştırma için)</summary>
        public decimal PreviousPeriodEarnings { get; set; }

        /// <summary>Önceki döneme göre yüzdelik değişim</summary>
        public double ChangePercent { get; set; }

        /// <summary>Günlük kazanç dökümü (grafik için)</summary>
        public List<DailyEarningDto> DailyBreakdown { get; set; } = new();
    }

    public class DailyEarningDto
    {
        /// <summary>Tarih (yyyy-MM-dd)</summary>
        public string Date { get; set; } = "";

        /// <summary>O güne ait kazanç</summary>
        public decimal Amount { get; set; }
    }
}
