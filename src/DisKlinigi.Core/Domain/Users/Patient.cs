using DisKlinigi.Core.Domain.Appointments;

namespace DisKlinigi.Core.Domain.Users;

/// <summary>Kendi kaydını oluşturup randevu alan hasta.</summary>
public class Patient : User
{
    /// <summary>İsteğe bağlı cep telefonu; şube son dakika değişikliklerinde aramak için.</summary>
    public string? PhoneNumber { get; private set; }

    private Patient()
    {
    }

    public Patient(string fullName, string email, string passwordHash, string? phoneNumber = null)
        : base(fullName, email, passwordHash)
    {
        ChangePhoneNumber(phoneNumber);
    }

    public override UserRole Role => UserRole.Patient;

    /// <summary>Hasta hiçbir şubeyi yönetemez.</summary>
    public override bool CanManageBranch(Branch branch) => false;

    /// <summary>Onay tıbbi bir karar; hasta hiçbir randevuyu, kendisininkini de onaylayamaz.</summary>
    public override bool CanApprove(Appointment appointment) => false;

    /// <summary>
    /// Hasta sadece kendi randevusunu ve randevuya 24 saatten fazla varken iptal edebilir
    /// (K4). Tam 24 saat kala artık edemez. Daha geç iptal şubenin işi, çünkü boşalan
    /// ünitin başka bir hastaya verilmesi gerekiyor.
    /// </summary>
    public override bool CanCancel(Appointment appointment, DateTime now) =>
        appointment.Patient.IsSameAs(this)
        && appointment.Period.Start - now > Appointment.PatientCancellationNotice;

    public void ChangePhoneNumber(string? phoneNumber)
    {
        PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
    }
}
