using DisKlinigi.Core.Booking;
using DisKlinigi.Core.Booking.Rules;
using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Appointments;
using DisKlinigi.Core.Domain.Users;
using Microsoft.Extensions.Time.Testing;

namespace DisKlinigi.Tests.Booking;

/// <summary>
/// Çakışma kuralı testleri için küçük bir klinik. Randevular 10 Kasım 2026 Salı gününe;
/// "şu an" bir gün önce, 9 Kasım Pazartesi 09:00.
/// </summary>
/// <remarks>
/// Serdivan bilerek 2 ünitli: ünit kapasitesi testlerinde üç farklı hekimin aynı saatte
/// randevusu olunca sınır aşılıyor. Kadıköy'de Periodontoloji yok (şube bölüm kuralı için).
///
/// Salı müsaitlikleri:
///   Ayşe (Endodonti)  — Serdivan 09:00–13:00, Kadıköy 14:00–18:00
///   Mehmet (Genel)    — Serdivan 09:00–18:00
///   Burak (Periodontoloji) — Serdivan 09:00–18:00
/// </remarks>
internal sealed class BookingClinic
{
    private const string Hash = "AQAAAAIAAYagAAAAE-ornek-ozet";

    /// <summary>Randevuların günü: 10 Kasım 2026 Salı.</summary>
    public static readonly DateOnly Tuesday = new(2026, 11, 10);

    /// <summary>9 Kasım 2026 Pazartesi 09:00.</summary>
    public static readonly DateTime ClockStart = new(2026, 11, 9, 9, 0, 0);

    public FakeTimeProvider Clock { get; }

    public Branch Serdivan { get; } = new("Serdivan Şubesi", "Sakarya", "", chairCount: 2);
    public Branch Kadikoy { get; } = new("Kadıköy Şubesi", "İstanbul", "", chairCount: 3);

    public Department Endodontics { get; } = new("Endodonti");
    public Department General { get; } = new("Genel Diş Hekimliği");
    public Department Periodontics { get; } = new("Periodontoloji");

    /// <summary>60 dk, onay gerektiriyor.</summary>
    public Service CanalTreatment { get; }

    /// <summary>20 dk, otomatik onaylı.</summary>
    public Service Examination { get; }

    /// <summary>30 dk, otomatik onaylı; Kadıköy'de bölümü yok.</summary>
    public Service Cleaning { get; }

    public Dentist Ayse { get; }
    public Dentist Mehmet { get; }
    public Dentist Burak { get; }

    public Patient Ali { get; } = new("Ali Yıldız", "ali@ornek.com", Hash);
    public Patient Zeynep { get; } = new("Zeynep Arslan", "zeynep@ornek.com", Hash);
    public Patient Can { get; } = new("Can Öztürk", "can@ornek.com", Hash);
    public Patient Deniz { get; } = new("Deniz Koç", "deniz@ornek.com", Hash);

    /// <summary>İptal için yetkili biri; zincir yöneticisi her randevuyu iptal edebiliyor.</summary>
    public ChainAdministrator Admin { get; } = new("Selin Aydın", "selin@ornek.com", Hash);

    public BookingClinic()
    {
        Clock = new FakeTimeProvider(new DateTimeOffset(ClockStart, TimeSpan.Zero));
        Clock.SetLocalTimeZone(TimeZoneInfo.Utc);

        Serdivan.SetWorkingHours(DayOfWeek.Tuesday, TimeOfDayRange.Of(9, 0, 18, 0));
        Serdivan.SetWorkingHours(DayOfWeek.Saturday, TimeOfDayRange.Of(9, 0, 14, 0));
        Serdivan.AddDepartment(Endodontics);
        Serdivan.AddDepartment(General);
        Serdivan.AddDepartment(Periodontics);

        Kadikoy.SetWorkingHours(DayOfWeek.Tuesday, TimeOfDayRange.Of(9, 0, 19, 0));
        Kadikoy.AddDepartment(Endodontics);
        Kadikoy.AddDepartment(General);

        CanalTreatment = new Service("Kanal tedavisi", Endodontics, TimeSpan.FromMinutes(60), 5000m, requiresApproval: true);
        Examination = new Service("Muayene", General, TimeSpan.FromMinutes(20), 750m, requiresApproval: false);
        Cleaning = new Service("Diş taşı temizliği", Periodontics, TimeSpan.FromMinutes(30), 1500m, requiresApproval: false);

        Ayse = new Dentist("Dt. Ayşe Kaya", "ayse@ornek.com", Hash, Endodontics);
        Ayse.AssignToBranch(Serdivan);
        Ayse.AssignToBranch(Kadikoy);
        Ayse.OfferService(CanalTreatment);
        Ayse.OfferService(Examination);
        Ayse.OfferService(Cleaning);
        Ayse.AddAvailability(Serdivan, DayOfWeek.Tuesday, TimeOfDayRange.Of(9, 0, 13, 0));
        Ayse.AddAvailability(Kadikoy, DayOfWeek.Tuesday, TimeOfDayRange.Of(14, 0, 18, 0));

        Mehmet = new Dentist("Dt. Mehmet Demir", "mehmet@ornek.com", Hash, General);
        Mehmet.AssignToBranch(Serdivan);
        Mehmet.OfferService(Examination);
        Mehmet.OfferService(Cleaning);
        Mehmet.AddAvailability(Serdivan, DayOfWeek.Tuesday, TimeOfDayRange.Of(9, 0, 18, 0));

        Burak = new Dentist("Dt. Burak Şen", "burak@ornek.com", Hash, Periodontics);
        Burak.AssignToBranch(Serdivan);
        Burak.OfferService(Examination);
        Burak.OfferService(Cleaning);
        Burak.AddAvailability(Serdivan, DayOfWeek.Tuesday, TimeOfDayRange.Of(9, 0, 18, 0));
    }

    /// <summary>Uygulamanın kullanacağı kurallar, tasarım.md bölüm 3'teki sırayla.</summary>
    public static IReadOnlyList<IBookingRule> AllRules() => BookingRules.CreateDefault();

    public DateTime Now => Clock.GetLocalNow().DateTime;

    /// <summary>Saati ileri sarar (FakeTimeProvider geri sarılamıyor).</summary>
    public void SetNow(DateTime moment) => Clock.SetUtcNow(new DateTimeOffset(moment, TimeSpan.Zero));

    /// <summary>10 Kasım Salı, verilen saat.</summary>
    public static DateTime At(int hour, int minute = 0) => Tuesday.ToDateTime(new TimeOnly(hour, minute));

    public BookingRequest Request(Patient patient, Dentist dentist, Service service, Branch branch, DateTime start) =>
        new(patient, dentist, service, branch, start);

    /// <summary>Sistemde zaten var olan randevu (kurallardan geçirilmeden oluşturuluyor).</summary>
    public static Appointment Existing(Patient patient, Dentist dentist, Service service, Branch branch, DateTime start) =>
        new(patient, dentist, service, branch, start, createdAt: start.AddDays(-3));

    /// <summary>Salı günü için, şu anki saatle kural bağlamı.</summary>
    public BookingContext Context(params Appointment[] appointments) => new(Now, Tuesday, appointments);

    public AppointmentScheduler Scheduler(InMemoryAppointmentRepository repository) =>
        new(repository, AllRules(), Clock);
}
