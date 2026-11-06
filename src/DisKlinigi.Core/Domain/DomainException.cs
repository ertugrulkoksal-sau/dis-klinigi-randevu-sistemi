namespace DisKlinigi.Core.Domain;

/// <summary>
/// Bir iş kuralı çiğnendiğinde fırlatılır (ör. ünit sayısının sıfır girilmesi,
/// onaylanmış randevunun tekrar onaylanması). Mesajlar doğrudan kullanıcıya
/// gösterilebilecek şekilde Türkçe yazılıyor.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}
