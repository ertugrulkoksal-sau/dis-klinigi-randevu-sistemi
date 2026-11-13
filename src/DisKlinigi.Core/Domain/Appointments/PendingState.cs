using DisKlinigi.Core.Domain.Users;

namespace DisKlinigi.Core.Domain.Appointments;

/// <summary>
/// Onay bekliyor: onay gerektiren hizmetlerde randevunun ilk durumu.
/// Onaylanabilir, reddedilebilir ya da iptal edilebilir.
/// </summary>
internal sealed class PendingState : ActiveState
{
    public static readonly PendingState Instance = new();

    private PendingState()
    {
    }

    public override AppointmentStatus Status => AppointmentStatus.PendingApproval;

    /// <summary>Başlamadan: onayla, reddet, iptal et. Saati geldiyse sadece reddet (K28).</summary>
    internal override AppointmentActions ActionsAt(Appointment appointment, DateTime now) =>
        appointment.HasStarted(now)
            ? AppointmentActions.Reject
            : base.ActionsAt(appointment, now) | AppointmentActions.Approve | AppointmentActions.Reject;

    /// <summary>
    /// Saati geçmiş bir randevuyu onaylamak anlamsız (hasta gelmiş ya da gelmemiş
    /// olabilir); böyle bir randevu ret ile kapatılıyor (K28).
    /// </summary>
    internal override void Approve(Appointment appointment, User by, DateTime now)
    {
        if (appointment.HasStarted(now))
            throw new DomainException("Saati gelmiş randevu onaylanamaz; reddedilerek kapatılmalıdır.");
        if (!by.CanApprove(appointment))
            throw new DomainException("Bu randevuyu onaylama yetkiniz yok.");

        appointment.TransitionTo(ConfirmedState.Instance);
    }

    internal override void Reject(Appointment appointment, User by)
    {
        if (!by.CanApprove(appointment))
            throw new DomainException("Bu randevuyu reddetme yetkiniz yok.");

        appointment.TransitionTo(FinalState.Rejected);
    }
}
