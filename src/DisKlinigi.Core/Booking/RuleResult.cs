namespace DisKlinigi.Core.Booking;

/// <summary>
/// Bir kuralın sonucu: geçti ya da kullanıcıya gösterilecek bir mesajla çiğnendi.
/// </summary>
/// <remarks>
/// Kural çiğnendiğinde istisna fırlatmak yerine sonuç döndürüyorum, çünkü planlayıcı
/// ilk hatada durmuyor, çiğnenen kuralların hepsini birlikte gösteriyor (tasarım.md bölüm 3).
/// Boş saat üretiminde de bir saatin dolu olması istisnai bir durum değil, olağan cevap.
/// </remarks>
public sealed class RuleResult
{
    public static readonly RuleResult Passed = new(null);

    private RuleResult(string? message)
    {
        Message = message;
    }

    /// <summary>Çiğnenen kuralın Türkçe açıklaması; kural geçtiyse null.</summary>
    public string? Message { get; }

    public bool IsViolated => Message is not null;

    public static RuleResult Violated(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Çiğnenen kuralın bir açıklaması olmalıdır.", nameof(message));
        return new RuleResult(message);
    }

    public override string ToString() => Message ?? "Geçti";
}
