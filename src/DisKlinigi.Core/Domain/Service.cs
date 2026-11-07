namespace DisKlinigi.Core.Domain;

/// <summary>
/// Bir bölüme ait tedavi ya da muayene (ör. Kanal tedavisi, 60 dk, 5.000 ₺).
/// </summary>
/// <remarks>
/// Onay gereksinimi bir alan olarak tutuluyor; "muayene" ve "tedavi" için ayrı alt
/// sınıflar açmadım çünkü aralarındaki tek davranış farkı bu (tasarım kararı K2).
/// Ücret değiştiğinde eski randevular etkilenmiyor; randevu kendi ücretinin
/// kopyasını saklıyor (K9).
/// </remarks>
public class Service : Entity
{
    /// <summary>Süreler 5 dakikanın katı; boş saat listesi çeyrek saatlik adımlarla üretileceği için daha ince süreye gerek yok.</summary>
    public const int DurationStepMinutes = 5;

    /// <summary>En uzun hizmet bir iş gününün yarısını geçmesin; daha uzun işlemler birkaç seansa bölünür.</summary>
    public const int MaxDurationMinutes = 240;

    public string Name { get; private set; } = null!;
    public Department Department { get; private set; } = null!;
    public TimeSpan Duration { get; private set; }
    public decimal Price { get; private set; }

    /// <summary>true ise randevu "Onay bekliyor" olarak oluşur, hekim ya da asistan onaylar.</summary>
    public bool RequiresApproval { get; private set; }

    private Service()
    {
    }

    public Service(string name, Department department, TimeSpan duration, decimal price, bool requiresApproval)
    {
        Rename(name);
        MoveTo(department);
        ChangeDuration(duration);
        ChangePrice(price);
        RequiresApproval = requiresApproval;
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Hizmet adı boş olamaz.");
        Name = name.Trim();
    }

    public void MoveTo(Department department)
    {
        Department = department ?? throw new DomainException("Hizmetin bir bölümü olmalıdır.");
    }

    public void ChangeDuration(TimeSpan duration)
    {
        if (duration.TotalMinutes < DurationStepMinutes || duration.TotalMinutes > MaxDurationMinutes)
            throw new DomainException($"Hizmet süresi {DurationStepMinutes} ile {MaxDurationMinutes} dakika arasında olmalıdır.");
        if (duration.Ticks % TimeSpan.FromMinutes(DurationStepMinutes).Ticks != 0)
            throw new DomainException($"Hizmet süresi {DurationStepMinutes} dakikanın katı olmalıdır.");
        Duration = duration;
    }

    public void ChangePrice(decimal price)
    {
        if (price < 0)
            throw new DomainException("Hizmet ücreti negatif olamaz.");
        Price = decimal.Round(price, 2);
    }

    public void SetApprovalRequirement(bool requiresApproval)
    {
        RequiresApproval = requiresApproval;
    }

    public override string ToString() => Name;
}
