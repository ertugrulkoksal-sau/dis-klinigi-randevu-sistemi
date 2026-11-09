namespace DisKlinigi.Core.Domain;

/// <summary>
/// Belirli bir tarihteki zaman aralığı, ör. 10 Kasım 2026 11:00–12:00.
/// Randevunun süresi bu tiple tutuluyor.
/// </summary>
/// <remarks>
/// Aralık yarı açık: [Start, End). Bitiş anı aralığa dahil değil; böylece
/// 10:00–10:30 ile 10:30–11:00 kesişmiyor ve arka arkaya randevu verilebiliyor.
/// Değer nesnesi (record): iki aralık başlangıç ve bitişi aynıysa eşit sayılıyor,
/// oluşturulduktan sonra değiştirilemiyor. Geçersiz aralık (bitiş başlangıçtan önce
/// ya da eşit) hiç oluşturulamıyor, bu yüzden kullanan kodun tekrar kontrol
/// etmesine gerek kalmıyor.
/// </remarks>
public sealed record TimeRange
{
    public DateTime Start { get; }
    public DateTime End { get; }

    public TimeRange(DateTime start, DateTime end)
    {
        if (end <= start)
            throw new DomainException("Bitiş zamanı başlangıç zamanından sonra olmalıdır.");
        Start = start;
        End = end;
    }

    public static TimeRange FromDuration(DateTime start, TimeSpan duration) => new(start, start + duration);

    public TimeSpan Duration => End - Start;

    /// <summary>
    /// İki aralığın ortak bir anı var mı? Yarı açık aralıkta bu, her birinin
    /// diğeri bitmeden başlaması demek. Uç uca eklenen aralıklar kesişmiyor.
    /// </summary>
    public bool Overlaps(TimeRange other) => Start < other.End && other.Start < End;

    /// <summary>Verilen aralık tamamen bu aralığın içinde mi? (Sınırlar dahil.)</summary>
    public bool Contains(TimeRange other) => Start <= other.Start && other.End <= End;

    /// <summary>Verilen an bu aralıkta mı? Bitiş anı dahil değil.</summary>
    public bool Contains(DateTime moment) => Start <= moment && moment < End;

    /// <summary>Sadece saatler, ör. "11:00–12:00"; tarihin zaten belli olduğu mesajlarda kullanılıyor.</summary>
    public string ToTimeString() => $"{Start:HH:mm}–{End:HH:mm}";

    public override string ToString() => $"{Start:dd.MM.yyyy HH:mm}–{End:HH:mm}";
}
