namespace DisKlinigi.Core.Booking;

/// <summary>
/// Randevu alınırken kontrol edilen tek bir kural (Strategy deseni, tasarım kararı K12).
/// </summary>
/// <remarks>
/// Her kural tek bir soruyu cevaplıyor ("hekim bu saatte başka randevuda mı?" gibi).
/// <see cref="AppointmentScheduler"/> kuralların listesini alıp hepsini çalıştırıyor;
/// hangi kuralın ne yaptığını bilmiyor. Yeni bir kural eklemek, mevcut kodu değiştirmeden
/// bu arayüzü uygulayan yeni bir sınıf yazıp listeye eklemek demek (Open/Closed).
/// Kurallar durumsuz; aynı örnek bütün isteklerde kullanılabiliyor.
/// </remarks>
public interface IBookingRule
{
    RuleResult Check(BookingRequest request, BookingContext context);
}
