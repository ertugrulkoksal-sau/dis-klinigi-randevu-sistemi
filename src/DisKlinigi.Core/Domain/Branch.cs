namespace DisKlinigi.Core.Domain;

/// <summary>
/// Zincirin bir şubesi. Tek klinikli bir kurum, tek şubeli bir zincir olarak
/// aynı modelle temsil ediliyor; ayrı bir "klinik" sınıfına gerek kalmıyor.
/// </summary>
/// <remarks>
/// Özellikler dışarıdan doğrudan değiştirilemiyor (private set). Değişiklik
/// sadece kuralları kontrol eden metotlarla yapılıyor; böylece geçersiz bir
/// şube nesnesi hiç oluşamıyor (kapsülleme). Çalışma saatleri ve bölümler de
/// dışarıya sadece okunur liste olarak açılıyor; listeye eleman eklemek için
/// şubenin metotlarından geçmek gerekiyor.
/// </remarks>
public class Branch : Entity
{
    private readonly List<DailyWorkingHours> _workingHours = [];
    private readonly List<Department> _departments = [];

    public string Name { get; private set; } = null!;
    public string City { get; private set; } = null!;
    public string Address { get; private set; } = null!;

    /// <summary>Şubedeki ünit (diş koltuğu) sayısı; aynı anda verilebilecek randevu sınırı.</summary>
    public int ChairCount { get; private set; }

    /// <summary>Açık olunan günler; kaydı olmayan gün kapalı.</summary>
    public IReadOnlyCollection<DailyWorkingHours> WorkingHours => _workingHours.AsReadOnly();

    /// <summary>Şubenin barındırdığı bölümler.</summary>
    public IReadOnlyCollection<Department> Departments => _departments.AsReadOnly();

    // EF Core nesneyi veritabanından okurken parametresiz yapıcıya ihtiyaç duyuyor.
    private Branch()
    {
    }

    public Branch(string name, string city, string address, int chairCount)
    {
        Rename(name);
        Relocate(city, address);
        ChangeChairCount(chairCount);
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Şube adı boş olamaz.");
        Name = name.Trim();
    }

    public void Relocate(string city, string address)
    {
        if (string.IsNullOrWhiteSpace(city))
            throw new DomainException("Şubenin bulunduğu il boş olamaz.");
        City = city.Trim();
        Address = address?.Trim() ?? string.Empty;
    }

    public void ChangeChairCount(int chairCount)
    {
        if (chairCount < 1)
            throw new DomainException("Şubede en az bir ünit (diş koltuğu) bulunmalıdır.");
        ChairCount = chairCount;
    }

    // ---- Çalışma saatleri ----

    /// <summary>
    /// Verilen günün çalışma saatini belirler. O gün için kayıt varsa saat
    /// değiştiriliyor, yoksa gün açılıyor; aynı güne iki kayıt oluşamıyor.
    /// </summary>
    public void SetWorkingHours(DayOfWeek day, TimeOfDayRange hours)
    {
        ArgumentNullException.ThrowIfNull(hours);

        var existing = _workingHours.FirstOrDefault(w => w.Day == day);
        if (existing is null)
            _workingHours.Add(new DailyWorkingHours(day, hours));
        else
            existing.Change(hours);
    }

    public void CloseOn(DayOfWeek day)
    {
        _workingHours.RemoveAll(w => w.Day == day);
    }

    /// <summary>O günün çalışma saati; şube o gün kapalıysa null.</summary>
    public TimeOfDayRange? GetWorkingHours(DayOfWeek day) =>
        _workingHours.FirstOrDefault(w => w.Day == day)?.Hours;

    /// <summary>
    /// Verilen aralık boyunca şube açık mı? Aralığın tamamı o günün açılış–kapanış
    /// saatine sığmalı; kapanıştan taşan ya da kapalı güne düşen aralık kabul edilmiyor.
    /// Randevu kurallarındaki "şube çalışma saati" kontrolü bunu kullanacak.
    /// </summary>
    public bool IsOpenDuring(TimeRange period)
    {
        var hours = GetWorkingHours(period.Start.DayOfWeek);
        return hours is not null && hours.Covers(period);
    }

    // ---- Bölümler ----

    /// <summary>Bölümü şubeye ekler; zaten varsa bir şey yapmaz (aynı bölüm iki kez eklenmez).</summary>
    public void AddDepartment(Department department)
    {
        ArgumentNullException.ThrowIfNull(department);
        if (!HostsDepartment(department))
            _departments.Add(department);
    }

    public void RemoveDepartment(Department department)
    {
        _departments.RemoveAll(d => d.IsSameAs(department));
    }

    public bool HostsDepartment(Department department) => _departments.Any(d => d.IsSameAs(department));

    public override string ToString() => Name;
}
