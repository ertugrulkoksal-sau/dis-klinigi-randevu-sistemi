namespace DisKlinigi.Core.Accounts;

/// <summary>Hasta kayıt formundan gelen bilgiler. Parola burada düz metin; özetlenip atılıyor, hiçbir yere yazılmıyor.</summary>
public sealed record PatientRegistration(string FullName, string Email, string Password, string? PhoneNumber = null);
