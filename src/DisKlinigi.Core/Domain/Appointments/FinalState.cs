namespace DisKlinigi.Core.Domain.Appointments;

/// <summary>
/// Son durumlar: Reddedildi, İptal edildi, Tamamlandı, Gelmedi. Hiçbir işleme izin
/// vermiyorlar ve saati bırakıyorlar.
/// </summary>
/// <remarks>
/// Dört durum davranış olarak tamamen aynı, sadece adları farklı. Bu yüzden dört boş
/// alt sınıf yerine tek sınıf ve dört örnek kullandım; davranışı farklı olan durumlar
/// (Onay bekliyor, Onaylandı) ayrı sınıf. İleride bir son durum farklı davranmaya
/// başlarsa (ör. "Gelmedi"den yeniden randevu açmak) kendi sınıfına ayrılır.
/// </remarks>
internal sealed class FinalState : AppointmentState
{
    public static readonly FinalState Rejected = new(AppointmentStatus.Rejected);
    public static readonly FinalState Cancelled = new(AppointmentStatus.Cancelled);
    public static readonly FinalState Completed = new(AppointmentStatus.Completed);
    public static readonly FinalState NoShow = new(AppointmentStatus.NoShow);

    private FinalState(AppointmentStatus status)
    {
        Status = status;
    }

    public override AppointmentStatus Status { get; }

    public override bool IsActive => false;
}
