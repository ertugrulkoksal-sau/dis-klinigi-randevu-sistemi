using DisKlinigi.Core.Domain;

namespace DisKlinigi.Core.Booking.Rules;

/// <summary>
/// Kural 7: randevu süresi boyunca şubede aynı anda yürüyen randevu sayısı ünit sayısını aşmamalı.
/// </summary>
/// <remarks>
/// "Kesişen randevu sayısı &lt; ünit sayısı" demek yanlış sonuç veriyor: 2 ünitli şubede
/// 09:00–09:30 ve 09:30–10:00 randevuları varken 09:00–10:00 isteği ikisiyle de kesişiyor
/// ama aynı anda hep tek randevu yürüyor, yani bir ünit boş. Bu yüzden istenen aralık
/// içindeki en yüksek eş zamanlı randevu sayısı (<see cref="PeakOccupancy"/>) hesaplanıyor.
/// </remarks>
public sealed class ChairCapacityRule : IBookingRule
{
    public RuleResult Check(BookingRequest request, BookingContext context)
    {
        var branch = request.Branch;
        var busy = context.ActiveOverlapping(request.Period)
            .Where(a => a.Branch.IsSameAs(branch))
            .Select(a => a.Period);

        return PeakOccupancy(busy, request.Period) < branch.ChairCount
            ? RuleResult.Passed
            : RuleResult.Violated($"Şubedeki {branch.ChairCount} ünitin hepsi bu saat aralığının bir kısmında dolu ({branch.Name}).");
    }

    /// <summary>
    /// Verilen aralık içinde aynı anda yürüyen en fazla randevu sayısı.
    /// </summary>
    /// <remarks>
    /// Her randevunun aralık içinde kalan kısmı için bir "başladı" (+1) ve bir "bitti" (−1)
    /// olayı yazılıyor; olaylar zamana göre sıralanıp sırayla toplanıyor, toplamın ulaştığı
    /// en yüksek değer cevap. Aynı anda biten ve başlayan iki randevu çakışmadığı için
    /// (yarı açık aralık) aynı andaki olaylarda önce bitişler sayılıyor. n randevu için
    /// O(n log n); bir şubenin bir günlük randevu sayısında bu zaten çok küçük.
    /// </remarks>
    public static int PeakOccupancy(IEnumerable<TimeRange> periods, TimeRange within)
    {
        var events = new List<(DateTime At, int Change)>();
        foreach (var period in periods.Where(p => p.Overlaps(within)))
        {
            events.Add((Later(period.Start, within.Start), +1));
            events.Add((Earlier(period.End, within.End), -1));
        }

        events.Sort((a, b) => a.At != b.At ? a.At.CompareTo(b.At) : a.Change.CompareTo(b.Change));

        int current = 0, peak = 0;
        foreach (var e in events)
        {
            current += e.Change;
            peak = Math.Max(peak, current);
        }
        return peak;
    }

    private static DateTime Later(DateTime a, DateTime b) => a > b ? a : b;

    private static DateTime Earlier(DateTime a, DateTime b) => a < b ? a : b;
}
