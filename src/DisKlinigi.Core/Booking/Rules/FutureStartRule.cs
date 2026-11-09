namespace DisKlinigi.Core.Booking.Rules;

/// <summary>Kural 1: geçmişe randevu yok. Başlangıç "şu an"dan sonra olmalı; tam şu an da kabul edilmiyor.</summary>
public sealed class FutureStartRule : IBookingRule
{
    public RuleResult Check(BookingRequest request, BookingContext context) =>
        request.Period.Start > context.Now
            ? RuleResult.Passed
            : RuleResult.Violated("Geçmiş bir saate randevu alınamaz.");
}
