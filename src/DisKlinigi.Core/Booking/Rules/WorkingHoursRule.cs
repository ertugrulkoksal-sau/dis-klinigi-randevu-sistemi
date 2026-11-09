using DisKlinigi.Core.Domain;

namespace DisKlinigi.Core.Booking.Rules;

/// <summary>
/// Kural 3: randevu, şubenin o günkü açılış–kapanış saatine tamamen sığmalı.
/// Kapanışa tam denk gelen bitiş kabul ediliyor, bir dakika taşan kabul edilmiyor.
/// </summary>
public sealed class WorkingHoursRule : IBookingRule
{
    public RuleResult Check(BookingRequest request, BookingContext context)
    {
        var branch = request.Branch;
        if (branch.IsOpenDuring(request.Period))
            return RuleResult.Passed;

        var day = request.Period.Start.DayOfWeek;
        var hours = branch.GetWorkingHours(day);
        return RuleResult.Violated(hours is null
            ? $"{branch.Name} {TurkishNames.Day(day)} günü kapalı."
            : $"Randevu saati ({request.Period.ToTimeString()}) {branch.Name} {TurkishNames.Day(day)} çalışma saatine ({hours}) sığmıyor.");
    }
}
