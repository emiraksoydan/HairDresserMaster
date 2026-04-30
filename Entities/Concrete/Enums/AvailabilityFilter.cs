namespace Entities.Concrete.Enums
{
    /// <summary>
    /// Tek anlam alanı: dükkan için &quot;açık&quot;, serbest berber için &quot;müsait&quot;.
    /// İlgili endpoint (mağaza / serbest berber) eşlemesini DAL uygular.
    /// </summary>
    public enum AvailabilityFilter
    {
        Any = 0,
        /// <summary>Mağaza: şu an açık; serbest berber: müsait.</summary>
        Ready = 1,
        /// <summary>Mağaza: kapalı; serbest berber: meşgul.</summary>
        NotReady = 2
    }
}
