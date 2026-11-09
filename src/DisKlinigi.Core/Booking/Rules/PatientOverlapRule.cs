namespace DisKlinigi.Core.Booking.Rules;

/// <summary>
/// Kural 6: hastanın, hangi şubede olursa olsun, bu saatle kesişen aktif randevusu olmamalı.
/// Hasta aynı saatte iki koltukta oturamaz; aynı hekimle ya da başka hekimle olması fark etmiyor.
/// </summary>
public sealed class PatientOverlapRule : IBookingRule
{
    public RuleResult Check(BookingRequest request, BookingContext context)
    {
        var clash = context.ActiveOverlapping(request.Period)
            .FirstOrDefault(a => a.Patient.IsSameAs(request.Patient));

        return clash is null
            ? RuleResult.Passed
            : RuleResult.Violated(
                $"Bu saatle çakışan başka bir randevunuz var: {clash.Period.ToTimeString()} {clash.ServiceNameAtBooking}, {clash.Branch.Name}.");
    }
}
