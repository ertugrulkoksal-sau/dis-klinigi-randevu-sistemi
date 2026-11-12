namespace DisKlinigi.Core.Booking.Rules;

/// <summary>
/// Kural 5: hekimin, hangi şubede olursa olsun, bu saatle kesişen aktif randevusu olmamalı.
/// </summary>
/// <remarks>
/// Şubeye bakılmıyor: hekim aynı anda iki şubede olamaz. Mesaj ekran tasarımındaki metinle
/// aynı (12 Kasım); diğer randevunun hastası, hizmeti ve saati yazmıyor, çünkü mesajı başka bir
/// hasta görüyor. Hasta zaten listeden yeni bir saat seçecek.
/// </remarks>
public sealed class DentistOverlapRule : IBookingRule
{
    public RuleResult Check(BookingRequest request, BookingContext context)
    {
        var busy = context.ActiveOverlapping(request.Period)
            .Any(a => a.Dentist.IsSameAs(request.Dentist));

        return busy
            ? RuleResult.Violated($"{request.Dentist.FullName} bu saat aralığında başka bir randevuda.")
            : RuleResult.Passed;
    }
}
