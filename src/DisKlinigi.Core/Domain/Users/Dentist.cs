using DisKlinigi.Core.Domain.Appointments;

namespace DisKlinigi.Core.Domain.Users;

/// <summary>
/// Diş hekimi. Bir uzmanlık bölümü, verdiği hizmetler, çalıştığı şubeler ve
/// haftalık müsaitlik aralıkları var. Bir hekim birden fazla şubede çalışabiliyor.
/// </summary>
/// <remarks>
/// Müsaitlik aralıkları hekimin içinde tutuluyor ve sadece hekim üzerinden
/// ekleniyor; aralık eklerken üç kural kontrol ediliyor:
/// (1) hekim o şubeye atanmış olmalı, (2) aralık şubenin o günkü çalışma saatine
/// tamamen sığmalı, (3) hekimin aynı gündeki başka bir aralığıyla — hangi şubede
/// olursa olsun — kesişmemeli. Üçüncü kural, randevu tarafındaki "hekim aynı anda
/// iki şubede olamaz" kuralının müsaitlik tarafındaki karşılığı.
/// </remarks>
public class Dentist : User
{
    private readonly List<Branch> _branches = [];
    private readonly List<Service> _services = [];
    private readonly List<AvailabilityWindow> _availability = [];

    /// <summary>Uzmanlık alanı. Verdiği hizmetler bununla sınırlı değil (ör. endodontist dolgu da yapabilir).</summary>
    public Department Specialty { get; private set; } = null!;

    public IReadOnlyCollection<Branch> Branches => _branches.AsReadOnly();
    public IReadOnlyCollection<Service> Services => _services.AsReadOnly();
    public IReadOnlyCollection<AvailabilityWindow> Availability => _availability.AsReadOnly();

    private Dentist()
    {
    }

    public Dentist(string fullName, string email, string passwordHash, Department specialty)
        : base(fullName, email, passwordHash)
    {
        ChangeSpecialty(specialty);
    }

    public override UserRole Role => UserRole.Dentist;

    /// <summary>Hekim şube ayarlarını yönetmez; kendi müsaitliğini ve randevularını yönetir.</summary>
    public override bool CanManageBranch(Branch branch) => false;

    /// <summary>Hekim sadece kendi randevularını onaylar; başka hekimin hastası hakkında karar vermez.</summary>
    public override bool CanApprove(Appointment appointment) => appointment.Dentist.IsSameAs(this);

    /// <summary>Hekim kendi randevusunu, son 24 saat dahil, iptal edebilir.</summary>
    public override bool CanCancel(Appointment appointment, DateTime now) => appointment.Dentist.IsSameAs(this);

    public void ChangeSpecialty(Department specialty)
    {
        Specialty = specialty ?? throw new DomainException("Hekimin uzmanlık bölümü belirtilmelidir.");
    }

    // ---- Şubeler ----

    public void AssignToBranch(Branch branch)
    {
        ArgumentNullException.ThrowIfNull(branch);
        if (!WorksAt(branch))
            _branches.Add(branch);
    }

    /// <summary>
    /// Hekimi şubeden çıkarır. O şubede müsaitlik aralığı varken çıkarılırsa
    /// sahipsiz aralıklar kalırdı; bu yüzden önce aralıkların silinmesi isteniyor.
    /// </summary>
    public void RemoveFromBranch(Branch branch)
    {
        if (_availability.Any(a => a.Branch.IsSameAs(branch)))
            throw new DomainException(
                $"{FullName} için bu şubede tanımlı müsaitlik aralıkları var ({branch.Name}); önce bu aralıklar silinmelidir.");
        _branches.RemoveAll(b => b.IsSameAs(branch));
    }

    public bool WorksAt(Branch branch) => _branches.Any(b => b.IsSameAs(branch));

    // ---- Hizmetler ----

    public void OfferService(Service service)
    {
        ArgumentNullException.ThrowIfNull(service);
        if (!Offers(service))
            _services.Add(service);
    }

    public void StopOfferingService(Service service)
    {
        _services.RemoveAll(s => s.IsSameAs(service));
    }

    public bool Offers(Service service) => _services.Any(s => s.IsSameAs(service));

    // ---- Müsaitlik ----

    public AvailabilityWindow AddAvailability(Branch branch, DayOfWeek day, TimeOfDayRange hours)
    {
        ArgumentNullException.ThrowIfNull(branch);
        ArgumentNullException.ThrowIfNull(hours);

        if (!WorksAt(branch))
            throw new DomainException($"{FullName} bu şubede çalışmıyor ({branch.Name}); önce şubeye atanmalıdır.");

        var dayName = TurkishNames.Day(day);
        var branchHours = branch.GetWorkingHours(day)
            ?? throw new DomainException($"{branch.Name} {dayName} günü kapalı.");

        if (!branchHours.Contains(hours))
            throw new DomainException(
                $"{hours} aralığı, {branch.Name} {dayName} çalışma saatine ({branchHours}) sığmıyor.");

        var clash = _availability.FirstOrDefault(a => a.Day == day && a.Hours.Overlaps(hours));
        if (clash is not null)
            throw new DomainException($"{hours} aralığı, tanımlı {clash} aralığıyla çakışıyor.");

        var window = new AvailabilityWindow(branch, day, hours);
        _availability.Add(window);
        return window;
    }

    public void RemoveAvailability(AvailabilityWindow window)
    {
        _availability.RemoveAll(a => a.IsSameAs(window));
    }

    /// <summary>Hekimin verilen şubede, verilen gündeki müsaitlik aralıkları (saate göre sıralı).</summary>
    public IReadOnlyList<AvailabilityWindow> AvailabilityAt(Branch branch, DayOfWeek day) =>
        _availability
            .Where(a => a.Branch.IsSameAs(branch) && a.Day == day)
            .OrderBy(a => a.Hours.Start)
            .ToList();

    /// <summary>
    /// Takvimdeki aralık, hekimin o şubedeki müsaitlik aralıklarından birine tamamen
    /// sığıyor mu? Randevu kurallarındaki "hekim müsaitliği" kontrolü bunu kullanacak.
    /// Uç uca iki aralığa bölünen randevu kabul edilmiyor (ör. aralıklar 09:00–13:00
    /// ve 13:00–18:00 iken 12:30–13:30). Uç uca aralıklar genelde farklı şubeler
    /// (sabah Serdivan, öğleden sonra Kadıköy) olduğu için aralıkları birleştirmedim.
    /// </summary>
    public bool IsAvailable(Branch branch, TimeRange period) =>
        _availability.Any(a => a.Covers(branch, period));

    /// <summary>
    /// Hasta bu hekimden bu şubede bu hizmet için randevu alabilir mi? Randevu ekranındaki hekim
    /// listesi bununla daraltılıyor: hekim şubede çalışıyor, hizmeti veriyor, şube hizmetin
    /// bölümünü barındırıyor ve hekimin o şubede en az bir müsaitlik aralığı var.
    /// </summary>
    /// <remarks>
    /// Kayıttaki kuralların (<c>DentistOffersServiceRule</c>, <c>BranchHostsDepartmentRule</c>,
    /// <c>DentistAvailabilityRule</c>) saatten bağımsız kısmı. Listede görünen ama hiç boş saati
    /// çıkmayan hekim olmasın diye müsaitliği olmayan hekim de eleniyor. Saate bağlı kurallar
    /// (çakışma, ünit) gün ve saat seçilince boş saat listesinde çalışıyor.
    /// </remarks>
    public bool CanServe(Branch branch, Service service)
    {
        ArgumentNullException.ThrowIfNull(branch);
        ArgumentNullException.ThrowIfNull(service);

        return WorksAt(branch)
            && Offers(service)
            && branch.HostsDepartment(service.Department)
            && _availability.Any(a => a.Branch.IsSameAs(branch));
    }

    /// <summary>
    /// <paramref name="from"/> gününden başlayarak <paramref name="days"/> gün içinde hekimin
    /// bu şubede müsaitlik aralığı olan tarihler. Randevu ekranındaki gün seçimi bu liste.
    /// </summary>
    public IReadOnlyList<DateOnly> WorkingDaysAt(Branch branch, DateOnly from, int days)
    {
        ArgumentNullException.ThrowIfNull(branch);
        ArgumentOutOfRangeException.ThrowIfNegative(days);

        var weekdays = _availability.Where(a => a.Branch.IsSameAs(branch)).Select(a => a.Day).ToHashSet();
        return Enumerable.Range(0, days)
            .Select(from.AddDays)
            .Where(date => weekdays.Contains(date.DayOfWeek))
            .ToList();
    }
}
