namespace Core.Utilities.Constants;

/// <summary>
/// Panel, hizmet ve paket fiyatları için TRY üst sınırı (decimal(18,2) ve iş kuralları ile uyumlu).
/// </summary>
public static class PriceLimits
{
    public const decimal MaxMonetaryTry = 999_999_999.99m;

    public const string MaxMonetaryTryMessage = "Tutar 999.999.999,99 ₺ değerini aşamaz.";
}
