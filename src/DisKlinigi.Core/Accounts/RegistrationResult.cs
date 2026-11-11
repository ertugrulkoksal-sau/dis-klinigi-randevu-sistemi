using DisKlinigi.Core.Domain.Users;

namespace DisKlinigi.Core.Accounts;

/// <summary>Kayıt formunda hatanın gösterileceği alan.</summary>
public enum RegistrationField
{
    /// <summary>Belli bir alana ait değil; formun üstünde gösteriliyor.</summary>
    None,
    Email,
    Password
}

/// <summary>
/// Hasta kaydının sonucu. Kayıt tutmaması olağan bir cevap (e-posta kullanılıyor, parola kısa),
/// bu yüzden istisna değil sonuç nesnesi dönüyor; randevu kurallarındaki <c>RuleResult</c> ile aynı fikir.
/// </summary>
public sealed class RegistrationResult
{
    private RegistrationResult(Patient? patient, string? error, RegistrationField field)
    {
        Patient = patient;
        Error = error;
        Field = field;
    }

    /// <summary>Kaydedilen hasta; kayıt tutmadıysa <c>null</c>.</summary>
    public Patient? Patient { get; }

    public string? Error { get; }

    public RegistrationField Field { get; }

    public bool Succeeded => Patient is not null;

    public static RegistrationResult Success(Patient patient) => new(patient, null, RegistrationField.None);

    public static RegistrationResult Failure(string error, RegistrationField field = RegistrationField.None) =>
        new(null, error, field);
}
