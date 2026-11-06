namespace DisKlinigi.Core.Domain;

/// <summary>
/// Zincirin bir şubesi. Tek klinikli bir kurum, tek şubeli bir zincir olarak
/// aynı modelle temsil ediliyor; ayrı bir "klinik" sınıfına gerek kalmıyor.
/// </summary>
/// <remarks>
/// Özellikler dışarıdan doğrudan değiştirilemiyor (private set). Değişiklik
/// sadece kuralları kontrol eden metotlarla yapılıyor; böylece geçersiz bir
/// şube nesnesi hiç oluşamıyor (kapsülleme).
/// Çalışma saatleri ve bölümler 7 Kasım'daki alan modeli adımında eklenecek.
/// </remarks>
public class Branch
{
    public int Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string City { get; private set; } = null!;
    public string Address { get; private set; } = null!;

    /// <summary>Şubedeki ünit (diş koltuğu) sayısı; aynı anda verilebilecek randevu sınırı.</summary>
    public int ChairCount { get; private set; }

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
}
