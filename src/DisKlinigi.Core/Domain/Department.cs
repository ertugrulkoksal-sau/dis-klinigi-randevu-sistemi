namespace DisKlinigi.Core.Domain;

/// <summary>
/// Diş hekimliği dalı (Endodonti, Ortodonti...). Zincir genelinde tek bir katalog
/// var; her şube bunlardan hangilerini barındırdığını kendisi seçiyor.
/// </summary>
public class Department : Entity
{
    public string Name { get; private set; } = null!;

    private Department()
    {
    }

    public Department(string name)
    {
        Rename(name);
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Bölüm adı boş olamaz.");
        Name = name.Trim();
    }

    public override string ToString() => Name;
}
