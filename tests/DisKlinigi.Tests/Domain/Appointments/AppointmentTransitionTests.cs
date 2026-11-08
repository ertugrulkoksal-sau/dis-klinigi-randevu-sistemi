using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Appointments;
using static DisKlinigi.Core.Domain.Appointments.AppointmentStatus;

namespace DisKlinigi.Tests.Domain.Appointments;

/// <summary>
/// Durum geçiş tablosunun her hücresi: 6 durum × 5 işlem = 30 test.
/// Geçerli 6 hücre dışındaki 24 hücrenin hepsi hata vermeli ve durumu değiştirmemeli.
/// </summary>
/// <remarks>
/// Geçersiz hücreleri tek tek yazmak yerine tüm durum × işlem çiftlerinden geçerli
/// olanları çıkararak üretiyorum. Böylece ileride bir durum ya da işlem eklenirse
/// yeni hücreler de kendiliğinden teste giriyor; hiçbir hücre unutulamıyor.
/// Geçersiz hücrelerde işlemi yetkili kişi (randevunun hekimi) ve işleme uygun
/// zamanda yapıyor; yani hatanın tek sebebi durum olmalı.
/// </remarks>
public class AppointmentTransitionTests
{
    public enum Operation { Approve, Reject, Cancel, Complete, MarkNoShow }

    private static readonly Dictionary<(AppointmentStatus From, Operation Op), AppointmentStatus> AllowedTransitions = new()
    {
        [(PendingApproval, Operation.Approve)] = Confirmed,
        [(PendingApproval, Operation.Reject)] = Rejected,
        [(PendingApproval, Operation.Cancel)] = Cancelled,
        [(Confirmed, Operation.Cancel)] = Cancelled,
        [(Confirmed, Operation.Complete)] = Completed,
        [(Confirmed, Operation.MarkNoShow)] = NoShow,
    };

    public static TheoryData<AppointmentStatus, Operation, AppointmentStatus> ValidCells()
    {
        var data = new TheoryData<AppointmentStatus, Operation, AppointmentStatus>();
        foreach (var ((from, op), to) in AllowedTransitions)
            data.Add(from, op, to);
        return data;
    }

    public static TheoryData<AppointmentStatus, Operation> InvalidCells()
    {
        var data = new TheoryData<AppointmentStatus, Operation>();
        foreach (var from in Enum.GetValues<AppointmentStatus>())
            foreach (var op in Enum.GetValues<Operation>())
                if (!AllowedTransitions.ContainsKey((from, op)))
                    data.Add(from, op);
        return data;
    }

    private readonly TestClinic _clinic = new();

    [Fact]
    public void Tablo_30_hucrenin_tamamini_kapsar()
    {
        Assert.Equal(6, ValidCells().Count());
        Assert.Equal(24, InvalidCells().Count());
    }

    [Theory]
    [MemberData(nameof(ValidCells))]
    public void Gecerli_gecis_randevuyu_yeni_duruma_tasir(AppointmentStatus from, Operation op, AppointmentStatus expected)
    {
        var appointment = ArrangeIn(from);

        Perform(op, appointment);

        Assert.Equal(expected, appointment.Status);
    }

    [Theory]
    [MemberData(nameof(InvalidCells))]
    public void Gecersiz_gecis_hata_verir_ve_durumu_degistirmez(AppointmentStatus from, Operation op)
    {
        var appointment = ArrangeIn(from);

        var ex = Assert.Throws<DomainException>(() => Perform(op, appointment));

        Assert.Equal(from, appointment.Status);
        // Hata zaman ya da yetkiden değil, durumdan kaynaklanmalı.
        Assert.StartsWith($"Durumu “{TurkishNames.Status(from)}” olan randevu", ex.Message);
    }

    [Theory]
    [InlineData(PendingApproval, true)]
    [InlineData(Confirmed, true)]
    [InlineData(Rejected, false)]
    [InlineData(Cancelled, false)]
    [InlineData(Completed, false)]
    [InlineData(NoShow, false)]
    public void Sadece_bekleyen_ve_onayli_randevu_saati_tutar(AppointmentStatus status, bool expected)
    {
        Assert.Equal(expected, ArrangeIn(status).IsActive);
        Assert.Equal(expected, AppointmentState.For(status).IsActive);
    }

    [Fact]
    public void Her_durum_degerinin_bir_durum_nesnesi_ve_Turkce_adi_var()
    {
        // Veritabanından okunan her değer bir durum nesnesine karşılık gelmeli.
        var statuses = Enum.GetValues<AppointmentStatus>();
        foreach (var status in statuses)
            Assert.Equal(status, AppointmentState.For(status).Status);

        var names = statuses.Select(TurkishNames.Status).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Fact]
    public void Tanimsiz_durum_degeri_kabul_edilmez()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AppointmentState.For((AppointmentStatus)99));
    }

    // ---- Yardımcılar ----

    /// <summary>Randevuyu, gerçekte olacağı gibi geçerli geçişlerle istenen duruma getirir.</summary>
    private Appointment ArrangeIn(AppointmentStatus status)
    {
        var dentist = _clinic.Ayse;
        switch (status)
        {
            case PendingApproval:
                return _clinic.Book(_clinic.CanalTreatment);
            case Confirmed:
                return _clinic.Book(_clinic.Examination);
            case Rejected:
                var rejected = _clinic.Book(_clinic.CanalTreatment);
                rejected.Reject(dentist, "Önce muayene gerekli.");
                return rejected;
            case Cancelled:
                var cancelled = _clinic.Book(_clinic.Examination);
                cancelled.Cancel(dentist, _clinic.Now);
                return cancelled;
            case Completed:
                var completed = _clinic.Book(_clinic.Examination);
                _clinic.MoveAtLeastTo(TestClinic.AppointmentStart.AddMinutes(30));
                completed.Complete(dentist, _clinic.Now);
                return completed;
            case NoShow:
                var noShow = _clinic.Book(_clinic.Examination);
                _clinic.MoveAtLeastTo(TestClinic.AppointmentStart.AddMinutes(30));
                noShow.MarkNoShow(dentist, _clinic.Now);
                return noShow;
            default:
                throw new ArgumentOutOfRangeException(nameof(status));
        }
    }

    /// <summary>İşlemi randevunun hekimi yapıyor; tamamlandı/gelmedi için saat randevu sonrasına sarılıyor.</summary>
    private void Perform(Operation op, Appointment appointment)
    {
        var dentist = _clinic.Ayse;
        switch (op)
        {
            case Operation.Approve:
                appointment.Approve(dentist, _clinic.Now);
                break;
            case Operation.Reject:
                appointment.Reject(dentist);
                break;
            case Operation.Cancel:
                appointment.Cancel(dentist, _clinic.Now);
                break;
            case Operation.Complete:
                _clinic.MoveAtLeastTo(TestClinic.AppointmentStart.AddMinutes(30));
                appointment.Complete(dentist, _clinic.Now);
                break;
            case Operation.MarkNoShow:
                _clinic.MoveAtLeastTo(TestClinic.AppointmentStart.AddMinutes(30));
                appointment.MarkNoShow(dentist, _clinic.Now);
                break;
        }
    }
}
