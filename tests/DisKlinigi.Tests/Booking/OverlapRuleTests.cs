using DisKlinigi.Core.Booking;
using DisKlinigi.Core.Booking.Rules;
using DisKlinigi.Core.Domain.Appointments;
using static DisKlinigi.Tests.Booking.BookingClinic;

namespace DisKlinigi.Tests.Booking;

/// <summary>
/// Kural 5 ve 6: hekim ve hasta çakışması. İstek her testte Ali'nin Ayşe Hanım'dan
/// Serdivan'da 10:00–11:00 kanal tedavisi.
/// </summary>
public class OverlapRuleTests
{
    private readonly BookingClinic _clinic = new();

    private BookingRequest Request() =>
        _clinic.Request(_clinic.Ali, _clinic.Ayse, _clinic.CanalTreatment, _clinic.Serdivan, At(10));

    private RuleResult Dentist(params Appointment[] existing) =>
        new DentistOverlapRule().Check(Request(), _clinic.Context(existing));

    private RuleResult Patient(params Appointment[] existing) =>
        new PatientOverlapRule().Check(Request(), _clinic.Context(existing));

    // ---- Kural 5: hekim çakışması ----

    [Fact]
    public void Hekimin_bos_gunu_kurali_gecer()
    {
        Assert.False(Dentist().IsViolated);
    }

    [Fact]
    public void Hekimin_kesisen_randevusu_varsa_reddedilir()
    {
        // 10:30–11:00: isteğin ikinci yarısıyla kesişiyor.
        var existing = Existing(_clinic.Zeynep, _clinic.Ayse, _clinic.Cleaning, _clinic.Serdivan, At(10, 30));

        var result = Dentist(existing);

        Assert.True(result.IsViolated);
        Assert.Equal("Dt. Ayşe Kaya bu saatte dolu (10:30–11:00, Serdivan Şubesi).", result.Message);
    }

    [Fact]
    public void Istegi_tamamen_kapsayan_randevu_da_cakisir()
    {
        var existing = Existing(_clinic.Zeynep, _clinic.Ayse, _clinic.CanalTreatment, _clinic.Serdivan, At(9, 30));
        // 09:30–10:30, istek 10:00–11:00.
        Assert.True(Dentist(existing).IsViolated);
    }

    [Fact]
    public void Istegin_icinde_kalan_kisa_randevu_da_cakisir()
    {
        var existing = Existing(_clinic.Zeynep, _clinic.Ayse, _clinic.Examination, _clinic.Serdivan, At(10, 20));
        // 10:20–10:40, istek 10:00–11:00.
        Assert.True(Dentist(existing).IsViolated);
    }

    [Fact]
    public void Hemen_once_biten_randevu_cakismaz()
    {
        // 09:00–10:00 ve 10:00–11:00: yarı açık aralık, uç uca randevu verilebiliyor.
        var before = Existing(_clinic.Zeynep, _clinic.Ayse, _clinic.CanalTreatment, _clinic.Serdivan, At(9));

        Assert.False(Dentist(before).IsViolated);
    }

    [Fact]
    public void Tam_bitis_aninda_baslayan_randevu_cakismaz()
    {
        var after = Existing(_clinic.Zeynep, _clinic.Ayse, _clinic.CanalTreatment, _clinic.Serdivan, At(11));

        Assert.False(Dentist(after).IsViolated);
    }

    [Fact]
    public void Hekimin_baska_subedeki_randevusu_da_cakisir()
    {
        // Hekim aynı anda iki şubede olamaz; kural şubeye bakmıyor.
        var elsewhere = Existing(_clinic.Zeynep, _clinic.Ayse, _clinic.Examination, _clinic.Kadikoy, At(10, 30));

        var result = Dentist(elsewhere);

        Assert.True(result.IsViolated);
        Assert.Contains("Kadıköy Şubesi", result.Message);
    }

    [Fact]
    public void Baska_hekimin_ayni_saatteki_randevusu_hekim_kuralini_etkilemez()
    {
        var colleague = Existing(_clinic.Zeynep, _clinic.Mehmet, _clinic.Examination, _clinic.Serdivan, At(10));

        Assert.False(Dentist(colleague).IsViolated);
    }

    [Fact]
    public void Onay_bekleyen_randevu_da_saati_tutar()
    {
        // K5: kanal tedavisi onay gerektiriyor; randevu Onay bekliyor durumunda ama saat dolu.
        var pending = Existing(_clinic.Zeynep, _clinic.Ayse, _clinic.CanalTreatment, _clinic.Serdivan, At(10));
        Assert.Equal(AppointmentStatus.PendingApproval, pending.Status);

        Assert.True(Dentist(pending).IsViolated);
    }

    [Fact]
    public void Iptal_edilen_randevu_saati_birakir()
    {
        var cancelled = Existing(_clinic.Zeynep, _clinic.Ayse, _clinic.CanalTreatment, _clinic.Serdivan, At(10));
        cancelled.Cancel(_clinic.Admin, _clinic.Now);

        Assert.False(Dentist(cancelled).IsViolated);
    }

    [Fact]
    public void Reddedilen_randevu_saati_birakir()
    {
        var rejected = Existing(_clinic.Zeynep, _clinic.Ayse, _clinic.CanalTreatment, _clinic.Serdivan, At(10));
        rejected.Reject(_clinic.Ayse, "Önce röntgen gerekiyor.");

        Assert.False(Dentist(rejected).IsViolated);
    }

    // ---- Kural 6: hasta çakışması ----

    [Fact]
    public void Hastanin_bos_saati_kurali_gecer()
    {
        Assert.False(Patient().IsViolated);
    }

    [Fact]
    public void Hastanin_baska_hekimle_kesisen_randevusu_varsa_reddedilir()
    {
        var other = Existing(_clinic.Ali, _clinic.Mehmet, _clinic.Examination, _clinic.Serdivan, At(10, 40));

        var result = Patient(other);

        Assert.True(result.IsViolated);
        Assert.Equal("Bu saatle çakışan başka bir randevunuz var: 10:40–11:00 Muayene, Serdivan Şubesi.", result.Message);
    }

    [Fact]
    public void Hastanin_baska_subedeki_randevusu_da_cakisir()
    {
        var other = Existing(_clinic.Ali, _clinic.Ayse, _clinic.Examination, _clinic.Kadikoy, At(9, 50));

        Assert.True(Patient(other).IsViolated);
    }

    [Fact]
    public void Hastanin_uc_uca_randevusu_cakismaz()
    {
        var before = Existing(_clinic.Ali, _clinic.Mehmet, _clinic.Examination, _clinic.Serdivan, At(9, 40));
        var after = Existing(_clinic.Ali, _clinic.Mehmet, _clinic.Examination, _clinic.Serdivan, At(11));

        Assert.False(Patient(before, after).IsViolated);
    }

    [Fact]
    public void Baska_hastanin_ayni_saatteki_randevusu_hasta_kuralini_etkilemez()
    {
        var someoneElse = Existing(_clinic.Zeynep, _clinic.Mehmet, _clinic.Examination, _clinic.Serdivan, At(10));

        Assert.False(Patient(someoneElse).IsViolated);
    }

    [Fact]
    public void Hastanin_iptal_ettigi_randevu_saati_birakir()
    {
        var cancelled = Existing(_clinic.Ali, _clinic.Mehmet, _clinic.Examination, _clinic.Serdivan, At(10));
        cancelled.Cancel(_clinic.Ali, _clinic.Now); // 25 saat var, hasta kendisi iptal edebiliyor.

        Assert.False(Patient(cancelled).IsViolated);
    }
}
