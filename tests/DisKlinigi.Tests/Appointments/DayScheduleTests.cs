using DisKlinigi.Core.Appointments;
using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Appointments;
using DisKlinigi.Core.Domain.Users;
using DisKlinigi.Tests.Booking;
using DisKlinigi.Tests.Domain.Appointments;

namespace DisKlinigi.Tests.Appointments;

/// <summary>
/// Günlük takvim (ekran 4a): randevular ve müsaitlik aralığının içinde kalan boş parçalar.
/// Gün 10 Kasım 2026 Salı; Dt. Ayşe Kaya Serdivan'da 09:00–13:00, Kadıköy'de 14:00–18:00 çalışıyor.
/// </summary>
public class DayScheduleTests
{
    private static readonly DateOnly Tuesday = new(2026, 11, 10);

    private readonly TestClinic _clinic = new();

    public DayScheduleTests()
    {
        _clinic.Serdivan.SetWorkingHours(DayOfWeek.Tuesday, TimeOfDayRange.Of(9, 0, 18, 0));
        _clinic.Kadikoy.SetWorkingHours(DayOfWeek.Tuesday, TimeOfDayRange.Of(9, 0, 18, 0));
        _clinic.Ayse.AssignToBranch(_clinic.Kadikoy);
        _clinic.Ayse.AddAvailability(_clinic.Serdivan, DayOfWeek.Tuesday, TimeOfDayRange.Of(9, 0, 13, 0));
        _clinic.Ayse.AddAvailability(_clinic.Kadikoy, DayOfWeek.Tuesday, TimeOfDayRange.Of(14, 0, 18, 0));
        _clinic.Ayse.OfferService(_clinic.Examination);
    }

    private Appointment At(int hour, int minute, Service service, Branch? branch = null, Patient? patient = null) =>
        new(patient ?? _clinic.Ali, _clinic.Ayse, service, branch ?? _clinic.Serdivan,
            Tuesday.ToDateTime(new TimeOnly(hour, minute)), createdAt: TestClinic.ClockStart);

    private static string Describe(DaySchedule day) =>
        string.Join(", ", day.Entries.Select(e => $"{e.Period.ToTimeString()} {(e.IsFree ? "boş" : e.Appointment!.ServiceNameAtBooking)}"));

    [Fact]
    public void Randevular_arasindaki_ve_aralik_sonundaki_bosluklar_listelenir()
    {
        var examination = At(9, 0, _clinic.Examination);                          // 09:00–09:20
        var canal = At(9, 30, _clinic.CanalTreatment, patient: _clinic.Zeynep);    // 09:30–10:30, onay bekliyor

        var day = DaySchedule.Build(_clinic.Ayse, Tuesday, [canal, examination], onlyAt: _clinic.Serdivan);

        Assert.Equal("09:00–09:20 Muayene, 09:20–09:30 boş, 09:30–10:30 Kanal tedavisi, 10:30–13:00 boş", Describe(day));
        Assert.Equal(2, day.AppointmentCount);
        Assert.Equal(1, day.PendingCount);
        Assert.Equal(TimeSpan.FromMinutes(10 + 150), day.FreeTime);
    }

    [Fact]
    public void Uc_uca_randevular_arasinda_bosluk_cikmaz()
    {
        // Yarı açık aralık: 09:00–09:20 ile 09:20–09:40 arasında boşluk yok.
        var first = At(9, 0, _clinic.Examination);
        var second = At(9, 20, _clinic.Examination, patient: _clinic.Zeynep);

        var day = DaySchedule.Build(_clinic.Ayse, Tuesday, [first, second], onlyAt: _clinic.Serdivan);

        Assert.Equal("09:00–09:20 Muayene, 09:20–09:40 Muayene, 09:40–13:00 boş", Describe(day));
    }

    [Fact]
    public void Iptal_ve_ret_saati_bosaltir_tamamlandi_ve_gelmedi_takvimde_kalir()
    {
        var cancelled = At(9, 0, _clinic.Examination);
        cancelled.Cancel(_clinic.Ayse, _clinic.Now);
        var rejected = At(10, 0, _clinic.CanalTreatment);
        rejected.Reject(_clinic.Ayse, "Önce muayene.");
        var completed = At(11, 0, _clinic.Examination);
        completed.Complete(_clinic.Ayse, completed.Period.Start);
        var noShow = At(12, 0, _clinic.Examination, patient: _clinic.Zeynep);
        noShow.MarkNoShow(_clinic.Ayse, noShow.Period.Start);

        var day = DaySchedule.Build(_clinic.Ayse, Tuesday, [cancelled, rejected, completed, noShow], onlyAt: _clinic.Serdivan);

        Assert.Equal("09:00–11:00 boş, 11:00–11:20 Muayene, 11:20–12:00 boş, 12:00–12:20 Muayene, 12:20–13:00 boş", Describe(day));
        Assert.Equal(2, day.AppointmentCount);
    }

    [Fact]
    public void Hekim_iki_subedeki_araligini_asistan_sadece_kendi_subesini_gorur()
    {
        var kadikoy = At(14, 0, _clinic.Examination, _clinic.Kadikoy);

        var dentistView = DaySchedule.Build(_clinic.Ayse, Tuesday, [kadikoy], onlyAt: null);
        var assistantView = DaySchedule.Build(_clinic.Ayse, Tuesday, [], onlyAt: _clinic.Serdivan);

        Assert.Equal("09:00–13:00 boş, 14:00–14:20 Muayene, 14:20–18:00 boş", Describe(dentistView));
        Assert.Equal(2, dentistView.Windows.Count);
        Assert.Equal("09:00–13:00 boş", Describe(assistantView));
    }

    [Fact]
    public void Calismadigi_gun_bos_takvimdir()
    {
        var day = DaySchedule.Build(_clinic.Ayse, Tuesday.AddDays(1), []);

        Assert.Empty(day.Windows);
        Assert.Empty(day.Entries);
        Assert.Equal(TimeSpan.Zero, day.FreeTime);
    }

    [Fact]
    public void Aralik_disina_dusen_randevu_yine_gosterilir()
    {
        // Aralık sonradan daraltılmış olabilir; randevu kayıtta duruyor, takvimden kaybolmamalı.
        var outside = At(13, 0, _clinic.Examination);

        var day = DaySchedule.Build(_clinic.Ayse, Tuesday, [outside], onlyAt: _clinic.Serdivan);

        Assert.Equal("09:00–13:00 boş, 13:00–13:20 Muayene", Describe(day));
    }

    [Fact]
    public async Task Asistan_takviminde_sadece_kendi_subesindeki_randevular_olur()
    {
        // Süzme AppointmentService'te, CanApprove ile: asistan Kadıköy randevusunu göremiyor.
        var serdivan = At(9, 0, _clinic.Examination);
        var kadikoy = At(14, 0, _clinic.Examination, _clinic.Kadikoy);
        var service = new AppointmentService(new InMemoryAppointmentRepository(serdivan, kadikoy), _clinic.Clock);

        var byAssistant = await service.GetDayAsync(_clinic.SerdivanAssistant, _clinic.Ayse, Tuesday);
        var byDentist = await service.GetDayAsync(_clinic.Ayse, _clinic.Ayse, Tuesday);

        Assert.Equal([serdivan], byAssistant.Entries.Where(e => !e.IsFree).Select(e => e.Appointment));
        Assert.Equal([serdivan, kadikoy], byDentist.Entries.Where(e => !e.IsFree).Select(e => e.Appointment));
    }
}
