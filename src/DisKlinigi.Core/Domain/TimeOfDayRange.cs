namespace DisKlinigi.Core.Domain;

/// <summary>
/// Tarihten bağımsız, günün saat aralığı, ör. 09:00–13:00. Şube çalışma
/// saatleri ve hekimin haftalık müsaitlik aralıkları bu tiple tutuluyor.
/// </summary>
/// <remarks>
/// <see cref="TimeRange"/> ile aynı kurallara sahip (yarı açık, değişmez,
/// geçersiz hali oluşturulamaz). İki ayrı tip kullanmamın sebebi "her salı
/// 09:00–13:00" ile "10 Kasım 11:00–12:00"nin farklı şeyler olması: birinci
/// tekrar eden bir kalıp, ikincisi takvimde tek bir an. Karşılaştırma gerektiğinde
/// <see cref="On"/> ile kalıp belirli bir güne yerleştiriliyor.
/// Gece yarısını geçen aralık desteklenmiyor; klinikler gece çalışmıyor.
/// </remarks>
public sealed record TimeOfDayRange
{
    public TimeOnly Start { get; }
    public TimeOnly End { get; }

    public TimeOfDayRange(TimeOnly start, TimeOnly end)
    {
        if (end <= start)
            throw new DomainException("Bitiş saati başlangıç saatinden sonra olmalıdır.");
        Start = start;
        End = end;
    }

    /// <summary>Kısa yazım: <c>TimeOfDayRange.Of(9, 0, 13, 0)</c>.</summary>
    public static TimeOfDayRange Of(int startHour, int startMinute, int endHour, int endMinute) =>
        new(new TimeOnly(startHour, startMinute), new TimeOnly(endHour, endMinute));

    public TimeSpan Duration => End - Start;

    public bool Overlaps(TimeOfDayRange other) => Start < other.End && other.Start < End;

    public bool Contains(TimeOfDayRange other) => Start <= other.Start && other.End <= End;

    /// <summary>Bu saat aralığını verilen güne yerleştirir: 09:00–13:00 + 10 Kasım → 10 Kasım 09:00–13:00.</summary>
    public TimeRange On(DateOnly date) => new(date.ToDateTime(Start), date.ToDateTime(End));

    /// <summary>
    /// Takvimdeki bir aralık, bu kalıbın o güne yerleştirilmiş haline tamamen sığıyor mu?
    /// Ertesi güne taşan aralık hiçbir zaman sığmaz.
    /// </summary>
    public bool Covers(TimeRange period) => On(DateOnly.FromDateTime(period.Start)).Contains(period);

    public override string ToString() => $"{Start:HH:mm}–{End:HH:mm}";
}
