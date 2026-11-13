namespace DisKlinigi.Core.Domain.Appointments;

/// <summary>
/// Randevu üzerinde yapılabilecek işlemler. Ekranda hangi butonların çıkacağı
/// <see cref="Appointment.ActionsFor"/> ile bu değerlerin birleşimi olarak soruluyor.
/// </summary>
[Flags]
public enum AppointmentActions
{
    None = 0,
    Approve = 1,
    Reject = 2,
    Cancel = 4,
    Complete = 8,
    MarkNoShow = 16
}
