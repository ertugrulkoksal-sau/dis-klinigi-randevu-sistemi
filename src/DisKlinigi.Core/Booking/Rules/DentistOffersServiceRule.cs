namespace DisKlinigi.Core.Booking.Rules;

/// <summary>Kural 2a: seçilen hizmet hekimin verdiği hizmetler arasında olmalı.</summary>
public sealed class DentistOffersServiceRule : IBookingRule
{
    public RuleResult Check(BookingRequest request, BookingContext context) =>
        request.Dentist.Offers(request.Service)
            ? RuleResult.Passed
            : RuleResult.Violated($"{request.Dentist.FullName} “{request.Service.Name}” hizmetini vermiyor.");
}
