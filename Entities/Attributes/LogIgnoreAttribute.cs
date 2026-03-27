namespace Entities.Attributes
{
    /// <summary>
    /// LogAspect tarafından serileştirilmemesi gereken property'lere uygulanır.
    /// Örnek: koordinatlar, hassas veriler.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class LogIgnoreAttribute : Attribute { }
}
