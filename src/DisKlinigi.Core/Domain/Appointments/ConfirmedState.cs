using DisKlinigi.Core.Domain.Users;

namespace DisKlinigi.Core.Domain.Appointments;

/// <summary>
/// Onaylandı: otomatik onaylı hizmetlerde ilk durum, diğerlerinde onaydan sonraki durum.
/// Saati gelmeden iptal edilebilir; saati geldikten sonra tamamlandı ya da gelmedi olarak kapatılır.
/// </summary>
internal sealed class ConfirmedState : ActiveState
{
    public static readonly ConfirmedState Instance = new();

    private ConfirmedState()
    {
    }

    public override AppointmentStatus Status => AppointmentStatus.Confirmed;

    /// <summary>Başlamadan sadece iptal; başlama anından itibaren tamamlandı ya da gelmedi.</summary>
    internal override AppointmentActions ActionsAt(Appointment appointment, DateTime now) =>
        appointment.HasStarted(now)
            ? AppointmentActions.Complete | AppointmentActions.MarkNoShow
            : base.ActionsAt(appointment, now);

    internal override void Complete(Appointment appointment, User by, DateTime now) =>
        RecordOutcome(appointment, by, now, FinalState.Completed);

    internal override void MarkNoShow(Appointment appointment, User by, DateTime now) =>
        RecordOutcome(appointment, by, now, FinalState.NoShow);

    /// <summary>
    /// Sonucu, randevuyu onaylama yetkisi olan kişi işliyor (hekimin kendisi ya da
    /// şubenin asistanı); ayrı bir yetki metodu açmadım çünkü iki sorunun cevabı her
    /// rolde aynı (K27). Randevu başlama anından itibaren kapatılabiliyor.
    /// </summary>
    private static void RecordOutcome(Appointment appointment, User by, DateTime now, FinalState outcome)
    {
        if (!appointment.HasStarted(now))
            throw new DomainException("Randevu saati gelmeden sonucu işlenemez.");
        if (!by.CanApprove(appointment))
            throw new DomainException("Bu randevunun sonucunu işleme yetkiniz yok.");

        appointment.TransitionTo(outcome);
    }
}
