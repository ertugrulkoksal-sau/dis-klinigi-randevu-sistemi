using DisKlinigi.Core.Domain;

namespace DisKlinigi.Core.Booking.Rules;

/// <summary>
/// Kural 4: randevu, hekimin o şubedeki o günkü müsaitlik aralıklarından birine tamamen sığmalı.
/// </summary>
/// <remarks>
/// Asıl kontrol <see cref="Domain.Users.Dentist.IsAvailable"/> içinde (K24); kural sadece
/// sonucu ve hastaya gösterilecek mesajı üretiyor. Mesajda hekimin o gün o şubedeki
/// aralıkları da yazıyor, böylece hasta başka bir saat seçerken neye bakacağını biliyor.
/// </remarks>
public sealed class DentistAvailabilityRule : IBookingRule
{
    public RuleResult Check(BookingRequest request, BookingContext context)
    {
        var dentist = request.Dentist;
        if (dentist.IsAvailable(request.Branch, request.Period))
            return RuleResult.Passed;

        var day = request.Period.Start.DayOfWeek;
        var windows = dentist.AvailabilityAt(request.Branch, day);
        return RuleResult.Violated(windows.Count == 0
            ? $"{dentist.FullName} {TurkishNames.Day(day)} günleri bu şubede çalışmıyor ({request.Branch.Name})."
            : $"Randevu saati ({request.Period.ToTimeString()}) {dentist.FullName} hekiminin bu şubedeki {TurkishNames.Day(day)} çalışma aralıklarına ({string.Join(", ", windows.Select(w => w.Hours))}) sığmıyor.");
    }
}
