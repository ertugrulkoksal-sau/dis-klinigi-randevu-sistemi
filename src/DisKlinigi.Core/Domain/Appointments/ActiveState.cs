using DisKlinigi.Core.Domain.Users;

namespace DisKlinigi.Core.Domain.Appointments;

/// <summary>
/// Saati tutan iki durumun (Onay bekliyor, Onaylandı) ortak tabanı. İkisinde de
/// iptal aynı kurallarla yapılıyor, bu yüzden iptal burada bir kez yazıldı.
/// </summary>
internal abstract class ActiveState : AppointmentState
{
    public override bool IsActive => true;

    /// <summary>
    /// İptal sadece randevu başlamadan yapılabiliyor. Saati gelmiş randevu iptal
    /// edilmiyor; onaylıysa tamamlandı/gelmedi, onay bekliyorsa ret ile kapatılıyor (K28).
    /// Kimin iptal edebileceği (hasta için 24 saat kuralı dahil) kullanıcı sınıfında.
    /// </summary>
    internal override void Cancel(Appointment appointment, User by, DateTime now)
    {
        if (appointment.HasStarted(now))
            throw new DomainException("Saati gelmiş randevu iptal edilemez.");

        if (!by.CanCancel(appointment, now))
        {
            // Hasta kendi randevusunu iptal edemiyorsa tek sebep 24 saat kuralı.
            throw new DomainException(appointment.Patient.IsSameAs(by)
                ? $"Randevuya {Appointment.PatientCancellationNotice.TotalHours:0} saatten az kaldığı için iptal sadece şube tarafından yapılabilir; lütfen şubeyi arayın."
                : "Bu randevuyu iptal etme yetkiniz yok.");
        }

        appointment.TransitionTo(FinalState.Cancelled);
    }
}
