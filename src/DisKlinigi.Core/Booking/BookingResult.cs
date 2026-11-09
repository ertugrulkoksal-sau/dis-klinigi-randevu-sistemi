using System.Diagnostics.CodeAnalysis;
using DisKlinigi.Core.Domain.Appointments;

namespace DisKlinigi.Core.Booking;

/// <summary>
/// Randevu alma denemesinin sonucu: ya kaydedilen randevu ya da çiğnenen kuralların listesi.
/// </summary>
public sealed class BookingResult
{
    private BookingResult(Appointment? appointment, IReadOnlyList<string> violations)
    {
        Appointment = appointment;
        Violations = violations;
    }

    /// <summary>Kaydedilen randevu. Durumu hizmete göre Onay bekliyor ya da Onaylandı (K2).</summary>
    public Appointment? Appointment { get; }

    /// <summary>Çiğnenen kuralların mesajları, kuralların sırasıyla. Başarılı sonuçta boş.</summary>
    public IReadOnlyList<string> Violations { get; }

    [MemberNotNullWhen(true, nameof(Appointment))]
    public bool Succeeded => Appointment is not null;

    internal static BookingResult Booked(Appointment appointment) => new(appointment, []);

    internal static BookingResult Rejected(IReadOnlyList<string> violations) => new(null, violations);
}
