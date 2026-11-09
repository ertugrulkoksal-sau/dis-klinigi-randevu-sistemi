namespace DisKlinigi.Core.Booking.Rules;

/// <summary>
/// Kural 5: hekimin, hangi şubede olursa olsun, bu saatle kesişen aktif randevusu olmamalı.
/// </summary>
/// <remarks>
/// Şubeye bakılmıyor: hekim aynı anda iki şubede olamaz. Mesajda diğer randevunun sadece
/// saati ve şubesi yazıyor; başka bir hastanın adı ya da hizmeti hastaya gösterilmiyor.
/// </remarks>
public sealed class DentistOverlapRule : IBookingRule
{
    public RuleResult Check(BookingRequest request, BookingContext context)
    {
        var clash = context.ActiveOverlapping(request.Period)
            .FirstOrDefault(a => a.Dentist.IsSameAs(request.Dentist));

        return clash is null
            ? RuleResult.Passed
            : RuleResult.Violated($"{request.Dentist.FullName} bu saatte dolu ({clash.Period.ToTimeString()}, {clash.Branch.Name}).");
    }
}
