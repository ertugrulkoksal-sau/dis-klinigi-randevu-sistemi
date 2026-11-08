using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Appointments;
using DisKlinigi.Core.Domain.Users;
using Microsoft.Extensions.Time.Testing;

namespace DisKlinigi.Tests.Domain.Appointments;

/// <summary>
/// Randevu testleri için küçük bir klinik: iki şube, iki hekim, iki hasta,
/// her şubede bir asistan ve bir şube yöneticisi, bir de zincir yöneticisi.
/// Randevular hep Serdivan'da, Dt. Ayşe Kaya'da ve 10 Kasım 11:00'de.
/// </summary>
/// <remarks>
/// "Şu an" <see cref="FakeTimeProvider"/> ile tutuluyor; uygulama da zamanı
/// <see cref="TimeProvider"/>'dan alacak (K16). FakeTimeProvider geriye
/// sarılamadığı için saat 8 Kasım sabahından başlıyor ve testler sadece ileri sarıyor.
/// </remarks>
internal sealed class TestClinic
{
    private const string Hash = "AQAAAAIAAYagAAAAE-ornek-ozet";

    /// <summary>10 Kasım 2026 Salı 11:00.</summary>
    public static readonly DateTime AppointmentStart = new(2026, 11, 10, 11, 0, 0);

    /// <summary>8 Kasım 2026 Pazar 09:00; randevuya 50 saat var.</summary>
    public static readonly DateTime ClockStart = new(2026, 11, 8, 9, 0, 0);

    public FakeTimeProvider Clock { get; }

    public Branch Serdivan { get; } = new("Serdivan Şubesi", "Sakarya", "", chairCount: 4);
    public Branch Kadikoy { get; } = new("Kadıköy Şubesi", "İstanbul", "", chairCount: 5);

    /// <summary>Onay gerektiren hizmet: randevu "Onay bekliyor" olarak oluşur.</summary>
    public Service CanalTreatment { get; }

    /// <summary>Otomatik onaylı hizmet: randevu doğrudan "Onaylandı" olarak oluşur.</summary>
    public Service Examination { get; }

    public Dentist Ayse { get; }
    public Dentist Mehmet { get; }
    public Patient Ali { get; } = new("Ali Yıldız", "ali@ornek.com", Hash);
    public Patient Zeynep { get; } = new("Zeynep Arslan", "zeynep@ornek.com", Hash);
    public Assistant SerdivanAssistant { get; }
    public Assistant KadikoyAssistant { get; }
    public BranchManager SerdivanManager { get; }
    public BranchManager KadikoyManager { get; }
    public ChainAdministrator Admin { get; } = new("Selin Aydın", "selin@ornek.com", Hash);

    public TestClinic()
    {
        Clock = new FakeTimeProvider(new DateTimeOffset(ClockStart, TimeSpan.Zero));
        Clock.SetLocalTimeZone(TimeZoneInfo.Utc);

        var endodontics = new Department("Endodonti");
        var general = new Department("Genel Diş Hekimliği");
        CanalTreatment = new Service("Kanal tedavisi", endodontics, TimeSpan.FromMinutes(60), 5000m, requiresApproval: true);
        Examination = new Service("Muayene", general, TimeSpan.FromMinutes(20), 750m, requiresApproval: false);

        Ayse = new Dentist("Dt. Ayşe Kaya", "ayse@ornek.com", Hash, endodontics);
        Mehmet = new Dentist("Dt. Mehmet Demir", "mehmet@ornek.com", Hash, general);
        // İki hekim de Serdivan'da çalışıyor; "başka hekim" aynı şubedeki bir meslektaş.
        Ayse.AssignToBranch(Serdivan);
        Mehmet.AssignToBranch(Serdivan);

        SerdivanAssistant = new Assistant("Elif Şahin", "elif@ornek.com", Hash, Serdivan);
        KadikoyAssistant = new Assistant("Can Öztürk", "can@ornek.com", Hash, Kadikoy);
        SerdivanManager = new BranchManager("Murat Çelik", "murat@ornek.com", Hash, Serdivan);
        KadikoyManager = new BranchManager("Deniz Koç", "deniz@ornek.com", Hash, Kadikoy);
    }

    /// <summary>Uygulamanın da yapacağı gibi yerel saati TimeProvider'dan okur (K10).</summary>
    public DateTime Now => Clock.GetLocalNow().DateTime;

    /// <summary>Saati verilen ana ileri sarar.</summary>
    public void SetNow(DateTime moment) => Clock.SetUtcNow(new DateTimeOffset(moment, TimeSpan.Zero));

    /// <summary>Saat zaten daha ilerideyse dokunmaz (FakeTimeProvider geri sarılamıyor).</summary>
    public void MoveAtLeastTo(DateTime moment)
    {
        if (Now < moment)
            SetNow(moment);
    }

    /// <summary>Ali'nin Ayşe Hanım'dan Serdivan'da 10 Kasım 11:00'e randevusu.</summary>
    public Appointment Book(Service service) => new(Ali, Ayse, service, Serdivan, AppointmentStart);

    /// <summary>Yetki testlerinde kullanılan kişiler; tablo satırlarında adlarıyla anılıyorlar.</summary>
    public User Actor(string name) => name switch
    {
        "Randevunun hastası" => Ali,
        "Başka hasta" => Zeynep,
        "Randevunun hekimi" => Ayse,
        "Başka hekim" => Mehmet,
        "Şubenin asistanı" => SerdivanAssistant,
        "Başka şubenin asistanı" => KadikoyAssistant,
        "Şube yöneticisi" => SerdivanManager,
        "Başka şubenin yöneticisi" => KadikoyManager,
        "Zincir yöneticisi" => Admin,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
    };
}
