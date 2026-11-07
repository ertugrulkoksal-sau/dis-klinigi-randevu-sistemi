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

    public void ChangePhoneNumber(string? phoneNumber)
    {
        PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
    }
}
