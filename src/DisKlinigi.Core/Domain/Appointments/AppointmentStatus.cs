namespace DisKlinigi.Core.Domain.Appointments;

/// <summary>
/// Randevunun durumu. Veritabanında bu değer saklanıyor; davranış (hangi işleme
/// izin verildiği) ise her değere karşılık gelen <see cref="AppointmentState"/>
/// sınıfında duruyor.
/// </summary>
public enum AppointmentStatus
{
    PendingApproval,
    Confirmed,
    Rejected,
    Cancelled,
    Completed,
    NoShow
}
