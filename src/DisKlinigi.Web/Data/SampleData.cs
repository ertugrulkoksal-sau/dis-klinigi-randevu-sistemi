using DisKlinigi.Core.Abstractions;
using DisKlinigi.Core.Booking;
using DisKlinigi.Core.Booking.Rules;
using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Appointments;
using DisKlinigi.Core.Domain.Users;

namespace DisKlinigi.Web.Data;

/// <summary>
/// Sunum ve deneme için örnek veri: 3 şube, 8 bölüm, 10 hizmet, 8 hekim, şube personeli,
/// 8 hasta ve geçmiş/bugün/gelecek randevular.
/// </summary>
/// <remarks>
/// Nesneler alan sınıflarının kendi metotlarıyla kuruluyor (<c>AddAvailability</c>,
/// <c>Approve</c>, <c>Complete</c>...); yani örnek veri de iş kurallarından geçiyor, veritabanına
/// elle satır yazılmıyor. Randevular bugünün tarihine göre yerleştiriliyor (K39): iki hafta öncesi,
/// geçen hafta, bugün, gelecek hafta ve ondan sonraki hafta. Böylece uygulama hangi gün
/// açılırsa açılsın takvimde, kuyrukta ve "Randevularım"da gösterilecek bir şey oluyor.
///
/// Her randevu, kayıttaki çakışma kurallarının hepsinden (geçmiş saat kuralı hariç) geçiriliyor.
/// Elle yazılan plan bir kuralı çiğnerse uygulama açılmıyor ve hangi randevunun neden
/// tutmadığı yazıyor; bunu bilerek yaptım, çünkü sessizce atlanan bir randevu sunumda
/// "neden bu saat boş?" sorusuna yol açardı.
/// </remarks>
public sealed class SampleData
{
    /// <summary>Bütün örnek hesapların parolası. Veritabanına sadece özeti yazılıyor (K15).</summary>
    public const string DemoPassword = "Demo1234!";

    /// <summary>Bugünün randevuları müsaitlik aralığının başından itibaren bu aralıklarla diziliyor.</summary>
    private static readonly TimeSpan TodayStep = TimeSpan.FromMinutes(90);

    private readonly IPasswordHasher _hasher;
    private readonly DateTime _now;
    private readonly DateOnly _monday;
    private readonly IReadOnlyList<IBookingRule> _rules;
    private readonly List<Appointment> _appointments = [];
    private readonly List<(Appointment Appointment, Outcome Outcome)> _outcomes = [];

    // Önceki randevularla birlikte kurallara verilen, saati tutmuş randevular. İptal edilen
    // ve reddedilen randevular saati bıraktığı için buraya girmiyor (K5).
    private readonly List<Appointment> _occupying = [];

    public List<Department> Departments { get; } = [];
    public List<Service> Services { get; } = [];
    public List<Branch> Branches { get; } = [];
    public List<User> Users { get; } = [];
    public IReadOnlyList<Appointment> Appointments => _appointments;

    private SampleData(IPasswordHasher hasher, DateTime now)
    {
        _hasher = hasher;
        _now = now;
        var today = DateOnly.FromDateTime(now);
        _monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));

        // Geçmiş saat kuralı dışında hepsi: örnek veride geçmiş randevular da olmalı.
        _rules = BookingRules.CreateDefault().Where(r => r is not FutureStartRule).ToList();
    }

    public static SampleData Create(IPasswordHasher hasher, DateTime now)
    {
        var data = new SampleData(hasher, now);
        data.Build();
        return data;
    }

    private void Build()
    {
        // ---- Bölümler ve hizmetler (tasarim.md 1.2 ve 1.3) ----
        var general = Department("Genel Diş Hekimliği");
        var surgery = Department("Ağız, Diş ve Çene Cerrahisi");
        var endodontics = Department("Endodonti");
        var orthodontics = Department("Ortodonti");
        var pedodontics = Department("Pedodonti");
        var periodontics = Department("Periodontoloji");
        var prosthetics = Department("Protetik Diş Tedavisi");
        var restorative = Department("Restoratif Diş Tedavisi");

        var examination = Service("Muayene", general, 20, 750m, approval: false);
        var checkUp = Service("Kontrol", general, 15, 500m, approval: false);
        var cleaning = Service("Diş taşı temizliği", periodontics, 30, 1500m, approval: false);
        var filling = Service("Kompozit dolgu", restorative, 45, 2500m, approval: true);
        var rootCanal = Service("Kanal tedavisi", endodontics, 60, 5000m, approval: true);
        var extraction = Service("Diş çekimi", surgery, 30, 2000m, approval: true);
        var implant = Service("İmplant uygulaması", surgery, 90, 25000m, approval: true);
        var orthoExam = Service("Ortodonti muayenesi", orthodontics, 30, 1000m, approval: false);
        var childExam = Service("Çocuk diş muayenesi", pedodontics, 20, 750m, approval: false);
        var crown = Service("Zirkonyum kron", prosthetics, 60, 8000m, approval: true);

        // ---- Şubeler ----
        var serdivan = Branch("Serdivan Şubesi", "Sakarya", "Kemalpaşa Mah. Üniversite Cad. No:12, Serdivan", 4,
            weekdays: TimeOfDayRange.Of(9, 0, 18, 0), saturday: TimeOfDayRange.Of(9, 0, 14, 0),
            general, surgery, endodontics, orthodontics, periodontics, restorative);
        var adapazari = Branch("Adapazarı Merkez Şubesi", "Sakarya", "Çark Cad. No:45, Adapazarı", 3,
            weekdays: TimeOfDayRange.Of(9, 0, 18, 0), saturday: null,
            general, endodontics, pedodontics, periodontics, prosthetics, restorative);
        var kadikoy = Branch("Kadıköy Şubesi", "İstanbul", "Bağdat Cad. No:210, Kadıköy", 5,
            weekdays: TimeOfDayRange.Of(10, 0, 19, 0), saturday: TimeOfDayRange.Of(10, 0, 16, 0),
            general, surgery, endodontics, orthodontics, pedodontics, periodontics, prosthetics, restorative);

        // ---- Hekimler: bazıları iki şubede, farklı günlerde çalışıyor ----
        var ayse = Dentist("Dt. Ayşe Kaya", "ayse.kaya", endodontics, [examination, checkUp, rootCanal, filling],
            (serdivan, DayOfWeek.Tuesday, TimeOfDayRange.Of(9, 0, 13, 0)),
            (kadikoy, DayOfWeek.Tuesday, TimeOfDayRange.Of(14, 0, 18, 0)),
            (serdivan, DayOfWeek.Thursday, TimeOfDayRange.Of(9, 0, 17, 0)),
            (kadikoy, DayOfWeek.Friday, TimeOfDayRange.Of(10, 0, 18, 0)));
        var mehmet = Dentist("Dt. Mehmet Demir", "mehmet.demir", general, [examination, checkUp, filling, cleaning],
            (serdivan, DayOfWeek.Monday, TimeOfDayRange.Of(9, 0, 17, 0)),
            (serdivan, DayOfWeek.Wednesday, TimeOfDayRange.Of(9, 0, 17, 0)),
            (serdivan, DayOfWeek.Friday, TimeOfDayRange.Of(9, 0, 17, 0)));
        var burak = Dentist("Dt. Burak Şen", "burak.sen", periodontics, [examination, checkUp, cleaning],
            (serdivan, DayOfWeek.Monday, TimeOfDayRange.Of(9, 0, 13, 0)),
            (adapazari, DayOfWeek.Monday, TimeOfDayRange.Of(14, 0, 18, 0)),
            (serdivan, DayOfWeek.Wednesday, TimeOfDayRange.Of(9, 0, 13, 0)),
            (adapazari, DayOfWeek.Wednesday, TimeOfDayRange.Of(14, 0, 18, 0)));
        var zehra = Dentist("Dt. Zehra Aksoy", "zehra.aksoy", surgery, [examination, extraction, implant],
            (serdivan, DayOfWeek.Tuesday, TimeOfDayRange.Of(9, 0, 18, 0)),
            (kadikoy, DayOfWeek.Thursday, TimeOfDayRange.Of(10, 0, 19, 0)),
            (serdivan, DayOfWeek.Saturday, TimeOfDayRange.Of(9, 0, 14, 0)));
        var emre = Dentist("Dt. Emre Yılmaz", "emre.yilmaz", orthodontics, [orthoExam, checkUp],
            (kadikoy, DayOfWeek.Monday, TimeOfDayRange.Of(10, 0, 19, 0)),
            (kadikoy, DayOfWeek.Wednesday, TimeOfDayRange.Of(10, 0, 19, 0)),
            (serdivan, DayOfWeek.Friday, TimeOfDayRange.Of(9, 0, 18, 0)));
        var gizem = Dentist("Dt. Gizem Polat", "gizem.polat", pedodontics, [childExam, checkUp, filling],
            (adapazari, DayOfWeek.Tuesday, TimeOfDayRange.Of(9, 0, 17, 0)),
            (adapazari, DayOfWeek.Thursday, TimeOfDayRange.Of(9, 0, 17, 0)));
        var okan = Dentist("Dt. Okan Erdem", "okan.erdem", prosthetics, [examination, checkUp, crown],
            (adapazari, DayOfWeek.Monday, TimeOfDayRange.Of(9, 0, 18, 0)),
            (adapazari, DayOfWeek.Friday, TimeOfDayRange.Of(9, 0, 18, 0)),
            (kadikoy, DayOfWeek.Saturday, TimeOfDayRange.Of(10, 0, 16, 0)));
        var selim = Dentist("Dt. Selim Kurt", "selim.kurt", restorative, [examination, checkUp, filling, rootCanal],
            (kadikoy, DayOfWeek.Tuesday, TimeOfDayRange.Of(10, 0, 19, 0)),
            (kadikoy, DayOfWeek.Thursday, TimeOfDayRange.Of(10, 0, 19, 0)),
            (kadikoy, DayOfWeek.Friday, TimeOfDayRange.Of(10, 0, 19, 0)));

        // ---- Şube personeli ve yöneticiler ----
        var elif = Staff(new Assistant("Elif Şahin", Email("elif.sahin"), Hash(), serdivan));
        var hakan = Staff(new Assistant("Hakan Tekin", Email("hakan.tekin"), Hash(), adapazari));
        var can = Staff(new Assistant("Can Öztürk", Email("can.ozturk"), Hash(), kadikoy));
        Staff(new BranchManager("Murat Çelik", Email("murat.celik"), Hash(), serdivan));
        Staff(new BranchManager("Seda Yurt", Email("seda.yurt"), Hash(), adapazari));
        Staff(new BranchManager("Deniz Koç", Email("deniz.koc"), Hash(), kadikoy));
        Staff(new ChainAdministrator("Selin Aydın", Email("selin.aydin"), Hash()));

        // ---- Hastalar ----
        var ali = Patient("Ali Yıldız", "ali.yildiz", "0532 111 22 33");
        var zeynep = Patient("Zeynep Arslan", "zeynep.arslan", "0533 222 33 44");
        var fatma = Patient("Fatma Kılıç", "fatma.kilic", null);
        var mert = Patient("Mert Doğan", "mert.dogan", "0541 333 44 55");
        var ece = Patient("Ece Güneş", "ece.gunes", null);
        var kerem = Patient("Kerem Bulut", "kerem.bulut", "0542 444 55 66");
        var ahmet = Patient("Ahmet Şimşek", "ahmet.simsek", "0505 555 66 77");
        var buse = Patient("Buse Karaca", "buse.karaca", null);
        var patients = new[] { ali, zeynep, fatma, mert, ece, kerem, ahmet, buse };

        // ---- İki hafta önce: çoğu tamamlandı; bir ret, bir hekim iptali, bir gelmedi ----
        Plan(-2, DayOfWeek.Monday, 9, 0, ali, mehmet, examination, serdivan, Outcome.Completed());
        Plan(-2, DayOfWeek.Monday, 10, 0, zeynep, burak, cleaning, serdivan, Outcome.Completed());
        Plan(-2, DayOfWeek.Tuesday, 9, 0, fatma, ayse, rootCanal, serdivan, Outcome.Completed(approvedBy: ayse));
        Plan(-2, DayOfWeek.Tuesday, 10, 0, mert, zehra, extraction, serdivan, Outcome.Completed(approvedBy: elif));
        Plan(-2, DayOfWeek.Wednesday, 11, 0, ece, emre, orthoExam, kadikoy, Outcome.NoShow());
        Plan(-2, DayOfWeek.Thursday, 10, 0, kerem, gizem, childExam, adapazari, Outcome.Completed());
        Plan(-2, DayOfWeek.Friday, 14, 0, ahmet, okan, crown, adapazari,
            Outcome.Rejected(okan, "Kron öncesi kanal tedavisi gerekiyor; lütfen önce Endodonti muayenesi alın."));
        Plan(-2, DayOfWeek.Friday, 11, 0, buse, selim, filling, kadikoy, Outcome.CancelledByStaff(approvedBy: selim, cancelledBy: selim));

        // ---- Geçen hafta: bir hasta iptali ve saati geçmiş, hâlâ onay bekleyen bir randevu (K28) ----
        Plan(-1, DayOfWeek.Monday, 9, 30, ali, mehmet, checkUp, serdivan, Outcome.Completed());
        Plan(-1, DayOfWeek.Tuesday, 11, 0, zeynep, ayse, examination, serdivan, Outcome.Completed());
        Plan(-1, DayOfWeek.Tuesday, 14, 0, ali, ayse, rootCanal, kadikoy, Outcome.Completed(approvedBy: can));
        Plan(-1, DayOfWeek.Wednesday, 15, 0, fatma, burak, cleaning, adapazari, Outcome.Completed());
        Plan(-1, DayOfWeek.Thursday, 13, 0, mert, zehra, implant, kadikoy, Outcome.Completed(approvedBy: zehra));
        Plan(-1, DayOfWeek.Thursday, 9, 0, kerem, gizem, filling, adapazari, Outcome.NoShow(approvedBy: hakan));
        Plan(-1, DayOfWeek.Friday, 10, 0, ece, emre, orthoExam, serdivan, Outcome.CancelledByPatient);
        Plan(-1, DayOfWeek.Saturday, 11, 0, buse, okan, examination, kadikoy, Outcome.Completed());
        Plan(-1, DayOfWeek.Friday, 16, 0, ahmet, selim, rootCanal, kadikoy, Outcome.Keep,
            note: "Geçen hafta dolgu yapılan dişte gece ağrısı başladı.");

        // ---- Bugün: hekimlerin bugünkü müsaitliğinden üretiliyor ----
        PlanToday([ayse, mehmet, burak, zehra, emre, gizem, okan, selim], patients, [elif, hakan, can]);

        // ---- Gelecek hafta: onay kuyruğunu dolduran bekleyen randevular, bir onay, bir ret ----
        Plan(1, DayOfWeek.Monday, 9, 0, zeynep, mehmet, examination, serdivan, Outcome.Keep);
        Plan(1, DayOfWeek.Monday, 11, 0, ali, burak, cleaning, serdivan, Outcome.Keep);
        Plan(1, DayOfWeek.Tuesday, 9, 0, ali, ayse, rootCanal, serdivan, Outcome.Keep,
            note: "Sağ üst dişimde sıcak-soğuk hassasiyeti var.");
        Plan(1, DayOfWeek.Tuesday, 10, 0, mert, zehra, implant, serdivan, Outcome.Approved(elif));
        Plan(1, DayOfWeek.Tuesday, 10, 0, fatma, ayse, filling, serdivan, Outcome.Keep);
        Plan(1, DayOfWeek.Wednesday, 10, 30, ece, emre, orthoExam, kadikoy, Outcome.Keep);
        Plan(1, DayOfWeek.Thursday, 9, 30, kerem, gizem, childExam, adapazari, Outcome.Keep,
            note: "Oğlum için alıyorum, 7 yaşında.");
        Plan(1, DayOfWeek.Thursday, 14, 0, buse, selim, filling, kadikoy,
            Outcome.Rejected(can, "Dolgudan önce muayene gerekiyor; lütfen önce muayene randevusu alın."));
        Plan(1, DayOfWeek.Friday, 9, 0, ahmet, okan, crown, adapazari, Outcome.Keep);
        Plan(1, DayOfWeek.Friday, 11, 0, zeynep, mehmet, checkUp, serdivan, Outcome.CancelledByPatient);

        // ---- İki hafta sonra ----
        Plan(2, DayOfWeek.Monday, 14, 0, fatma, burak, checkUp, adapazari, Outcome.Keep);
        Plan(2, DayOfWeek.Tuesday, 15, 0, buse, ayse, examination, kadikoy, Outcome.Keep);
        Plan(2, DayOfWeek.Wednesday, 9, 0, ali, mehmet, filling, serdivan, Outcome.Keep);
        Plan(2, DayOfWeek.Thursday, 10, 0, mert, zehra, extraction, kadikoy, Outcome.Keep,
            note: "Yirmi yaş dişi; panoramik röntgenim yanımda.");
        Plan(2, DayOfWeek.Saturday, 10, 0, ece, zehra, examination, serdivan, Outcome.Keep);
        Plan(2, DayOfWeek.Friday, 15, 0, kerem, emre, checkUp, serdivan, Outcome.Keep);

        // Durum değişiklikleri en sonda: kural kontrolü her randevuyu alındığı andaki gibi,
        // henüz tamamlanmamış/iptal edilmemiş haliyle görüyor.
        foreach (var (appointment, outcome) in _outcomes)
            outcome.Apply(appointment);
    }

    // ---- Bugünün randevuları ----

    /// <summary>
    /// Bugün çalışan her hekim için müsaitlik aralığının başından itibaren 90 dakikada bir
    /// randevu. Hizmetler hekimin hizmetleri arasında sırayla, hastalar da sırayla seçiliyor;
    /// sıradaki hasta o saatte başka randevudaysa bir sonraki deneniyor. Saati geçmiş olanlar
    /// tamamlandı (her dördüncüsü gelmedi), şu an süren onaylı, ileridekiler onaylı ya da
    /// onay bekliyor. Gün Pazar ise ya da kimse çalışmıyorsa bugün randevu yok.
    /// </summary>
    private void PlanToday(Dentist[] dentists, Patient[] patients, Assistant[] assistants)
    {
        var today = DateOnly.FromDateTime(_now);
        var nextPatient = 0;

        foreach (var dentist in dentists)
        {
            foreach (var window in dentist.Availability.Where(w => w.Day == today.DayOfWeek).OrderBy(w => w.Hours.Start))
            {
                var hours = window.Hours.On(today);
                var services = dentist.Services.Where(s => window.Branch.HostsDepartment(s.Department)).ToList();
                var assistant = assistants.First(a => a.Branch.IsSameAs(window.Branch));

                var k = 0;
                for (var start = hours.Start; ; start += TodayStep, k++)
                {
                    var service = services[k % services.Count];
                    if (start + service.Duration > hours.End)
                        break;

                    var patient = Enumerable.Range(0, patients.Length)
                        .Select(i => patients[(nextPatient + i) % patients.Length])
                        .FirstOrDefault(p => Violations(new BookingRequest(p, dentist, service, window.Branch, start)).Count == 0);
                    if (patient is null)
                        continue;
                    nextPatient++;

                    var slot = k;
                    Add(new BookingRequest(patient, dentist, service, window.Branch, start),
                        new Outcome(a => TodayOutcome(a, slot, assistant)));
                }
            }
        }
    }

    private void TodayOutcome(Appointment a, int slot, Assistant assistant)
    {
        var started = a.HasStarted(_now);
        var ended = a.Period.End <= _now;

        if (a.Status == AppointmentStatus.PendingApproval && (started || slot % 2 == 1))
            Outcome.Approve(a, slot % 2 == 0 ? a.Dentist : assistant);

        if (ended)
        {
            if (slot % 4 == 3) a.MarkNoShow(a.Dentist, a.Period.End);
            else a.Complete(a.Dentist, a.Period.End);
        }
    }

    // ---- Plan yardımcıları ----

    private void Plan(int week, DayOfWeek day, int hour, int minute,
        Patient patient, Dentist dentist, Service service, Branch branch,
        Outcome outcome, string? note = null)
    {
        var start = _monday.AddDays(7 * week + ((int)day + 6) % 7).ToDateTime(new TimeOnly(hour, minute));
        var request = new BookingRequest(patient, dentist, service, branch, start, note);

        var violations = Violations(request);
        if (violations.Count > 0)
            throw new InvalidOperationException(
                $"Örnek veri tutarsız: {request} kurallara uymuyor: {string.Join(" ", violations)}");

        Add(request, outcome);
    }

    private List<string> Violations(BookingRequest request)
    {
        var context = new BookingContext(_now, request.Date, _occupying);
        return _rules
            .Select(rule => rule.Check(request, context))
            .Where(result => result.IsViolated)
            .Select(result => result.Message!)
            .ToList();
    }

    /// <summary>
    /// Randevuyu, alındığı an olarak başlangıçtan 4 gün önceyi vererek oluşturur; bu an
    /// gelecekte kalıyorsa (iki hafta sonraki randevular) iki saat öncesi alınıyor.
    /// </summary>
    private void Add(BookingRequest request, Outcome outcome)
    {
        var createdAt = Earlier(request.Period.Start.AddDays(-4), _now.AddHours(-2));
        var appointment = new Appointment(
            request.Patient, request.Dentist, request.Service, request.Branch, request.Period.Start,
            createdAt, request.PatientNote);

        _appointments.Add(appointment);
        _outcomes.Add((appointment, outcome));
        if (!outcome.ReleasesSlot)
            _occupying.Add(appointment);
    }

    // ---- Randevunun sonraki durumu ----

    /// <summary>
    /// Plandaki randevunun alındıktan sonra başına gelenler. Durum değişiklikleri alan
    /// metotlarıyla ve geçmişteki uygun anlarla yapılıyor: onay alındıktan 1 saat sonra,
    /// sonuç randevu bitince, hasta iptali alındıktan 1 saat sonra (24 saat kuralını geçiyor).
    /// İptal ve ret saati bıraktığı için (<see cref="ReleasesSlot"/>) sonraki randevuların
    /// kural kontrolünde sayılmıyor.
    /// </summary>
    private sealed record Outcome(Action<Appointment> Apply, bool ReleasesSlot = false)
    {
        /// <summary>Durum değişmiyor: otomatik onaylı hizmette onaylı, diğerlerinde onay bekliyor kalıyor.</summary>
        public static readonly Outcome Keep = new(_ => { });

        public static readonly Outcome CancelledByPatient =
            new(a => a.Cancel(a.Patient, a.CreatedAt.AddHours(1)), ReleasesSlot: true);

        public static Outcome Approved(User by) => new(a => Approve(a, by));

        public static Outcome Completed(User? approvedBy = null) =>
            new(a => { ApproveIfNeeded(a, approvedBy); a.Complete(a.Dentist, a.Period.End); });

        public static Outcome NoShow(User? approvedBy = null) =>
            new(a => { ApproveIfNeeded(a, approvedBy); a.MarkNoShow(a.Dentist, a.Period.End); });

        public static Outcome Rejected(User by, string reason) => new(a => a.Reject(by, reason), ReleasesSlot: true);

        public static Outcome CancelledByStaff(User approvedBy, User cancelledBy) =>
            new(a => { Approve(a, approvedBy); a.Cancel(cancelledBy, a.CreatedAt.AddHours(2)); }, ReleasesSlot: true);

        public static void Approve(Appointment a, User by) => a.Approve(by, a.CreatedAt.AddHours(1));

        private static void ApproveIfNeeded(Appointment a, User? by)
        {
            if (by is not null)
                Approve(a, by);
        }
    }

    // ---- Nesne oluşturma yardımcıları ----

    private Department Department(string name)
    {
        var department = new Department(name);
        Departments.Add(department);
        return department;
    }

    private Service Service(string name, Department department, int minutes, decimal price, bool approval)
    {
        var service = new Service(name, department, TimeSpan.FromMinutes(minutes), price, approval);
        Services.Add(service);
        return service;
    }

    private Branch Branch(string name, string city, string address, int chairs,
        TimeOfDayRange weekdays, TimeOfDayRange? saturday, params Department[] departments)
    {
        var branch = new Branch(name, city, address, chairs);
        foreach (var day in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })
            branch.SetWorkingHours(day, weekdays);
        if (saturday is not null)
            branch.SetWorkingHours(DayOfWeek.Saturday, saturday);
        foreach (var department in departments)
            branch.AddDepartment(department);

        Branches.Add(branch);
        return branch;
    }

    private Dentist Dentist(string fullName, string login, Department specialty, Service[] services,
        params (Branch Branch, DayOfWeek Day, TimeOfDayRange Hours)[] availability)
    {
        var dentist = new Dentist(fullName, Email(login), Hash(), specialty);
        foreach (var service in services)
            dentist.OfferService(service);
        foreach (var (branch, day, hours) in availability)
        {
            dentist.AssignToBranch(branch);
            dentist.AddAvailability(branch, day, hours);
        }

        Users.Add(dentist);
        return dentist;
    }

    private T Staff<T>(T user) where T : User
    {
        Users.Add(user);
        return user;
    }

    private Patient Patient(string fullName, string login, string? phone)
    {
        var patient = new Patient(fullName, $"{login}@ornek.test", Hash(), phone);
        Users.Add(patient);
        return patient;
    }

    /// <summary>
    /// Örnek adresler <c>.test</c> uzantılı: bu uzantı deneme için ayrılmış, gerçek bir kişiye
    /// e-posta gitmesi mümkün değil.
    /// </summary>
    private static string Email(string login) => $"{login}@disklinigi.test";

    /// <summary>Her hesap ayrı özetleniyor; tuz rastgele olduğu için aynı parolanın özetleri farklı.</summary>
    private string Hash() => _hasher.Hash(DemoPassword);

    private static DateTime Earlier(DateTime a, DateTime b) => a < b ? a : b;
}
