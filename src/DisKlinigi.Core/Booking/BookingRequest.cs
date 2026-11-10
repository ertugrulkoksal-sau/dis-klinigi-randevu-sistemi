using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Appointments;
using DisKlinigi.Core.Domain.Users;

namespace DisKlinigi.Core.Booking;

/// <summary>
/// Henüz kaydedilmemiş bir randevu isteği: hasta, hekim, hizmet, şube ve başlangıç saati.
/// Kurallar bu nesneye bakıyor; her şey kurallardan geçerse planlayıcı bundan
/// bir <see cref="Domain.Appointments.Appointment"/> oluşturuyor.
/// </summary>
/// <remarks>
/// Bitiş, randevu sınıfındaki gibi hizmet süresinden hesaplanıyor; böylece kuralların
/// kontrol ettiği aralık ile kaydedilen randevunun aralığı aynı oluyor.
/// </remarks>
public sealed class BookingRequest
{
    public Patient Patient { get; }
    public Dentist Dentist { get; }
    public Service Service { get; }
    public Branch Branch { get; }
    public TimeRange Period { get; }

    /// <summary>Hastanın hekime notu (K35); kurallar buna bakmıyor, randevuya olduğu gibi geçiyor.</summary>
    public string? PatientNote { get; }

    public BookingRequest(
        Patient patient, Dentist dentist, Service service, Branch branch, DateTime start, string? patientNote = null)
    {
        Patient = patient ?? throw new DomainException("Randevunun hastası belirtilmelidir.");
        Dentist = dentist ?? throw new DomainException("Randevunun hekimi belirtilmelidir.");
        Service = service ?? throw new DomainException("Randevunun hizmeti belirtilmelidir.");
        Branch = branch ?? throw new DomainException("Randevunun şubesi belirtilmelidir.");
        Period = TimeRange.FromDuration(start, service.Duration);
        PatientNote = Appointment.NormalizePatientNote(patientNote);
    }

    /// <summary>Randevunun günü; kurallar bu günün randevularıyla çalışıyor.</summary>
    public DateOnly Date => DateOnly.FromDateTime(Period.Start);

    /// <summary>Aynı hasta, hekim, hizmet ve şube için başka bir başlangıç saati (boş saat üretiminde).</summary>
    public BookingRequest At(DateTime start) => new(Patient, Dentist, Service, Branch, start, PatientNote);

    public override string ToString() => $"{Period} {Service.Name}, {Dentist.FullName}, {Branch.Name}";
}
