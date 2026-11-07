namespace DisKlinigi.Core.Domain.Users;

/// <summary>
/// Hekimin belirli bir şubede, haftanın belirli bir günü çalıştığı saat aralığı
/// (ör. Salı 09:00–13:00, Serdivan). Her hafta tekrar ediyor (tasarım kararı K8).
/// </summary>
/// <remarks>
/// Yapıcı internal: aralık sadece <see cref="Dentist.AddAvailability"/> üzerinden
/// oluşturulabiliyor. Böylece "şube kapalıyken aralık" ya da "kesişen iki aralık"
/// gibi kurallar atlanamıyor; kontrol tek yerde, hekim sınıfında.
/// </remarks>
public class AvailabilityWindow : Entity
{
    public Branch Branch { get; private set; } = null!;
    public DayOfWeek Day { get; private set; }
    public TimeOfDayRange Hours { get; private set; } = null!;

    private AvailabilityWindow()
    {
    }

    internal AvailabilityWindow(Branch branch, DayOfWeek day, TimeOfDayRange hours)
    {
        Branch = branch;
        Day = day;
        Hours = hours;
    }

    /// <summary>Takvimdeki aralık bu müsaitliğe (şube, gün ve saat olarak) tamamen sığıyor mu?</summary>
    public bool Covers(Branch branch, TimeRange period) =>
        Branch.IsSameAs(branch) && period.Start.DayOfWeek == Day && Hours.Covers(period);

    public override string ToString() => $"{TurkishNames.Day(Day)} {Hours}, {Branch.Name}";
}
