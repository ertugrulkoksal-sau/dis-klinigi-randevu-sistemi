using DisKlinigi.Core.Booking.Rules;

namespace DisKlinigi.Core.Booking;

/// <summary>
/// Uygulamanın kullandığı randevu kuralları, tasarım.md bölüm 3'teki sırayla.
/// </summary>
/// <remarks>
/// Liste tek yerde: uygulama DI'a bu sırayla kaydediyor, testler ve örnek veri de aynı
/// listeyi kullanıyor. Sıra önemli, çünkü çiğnenen kuralların mesajları bu sırayla
/// gösteriliyor. Yeni kural eklemek yeni bir sınıf yazıp buraya bir satır eklemek demek;
/// planlayıcının kodu değişmiyor (K12).
/// </remarks>
public static class BookingRules
{
    public static IReadOnlyList<IBookingRule> CreateDefault() =>
    [
        new FutureStartRule(),
        new DentistOffersServiceRule(),
        new BranchHostsDepartmentRule(),
        new WorkingHoursRule(),
        new DentistAvailabilityRule(),
        new DentistOverlapRule(),
        new PatientOverlapRule(),
        new ChairCapacityRule()
    ];
}
