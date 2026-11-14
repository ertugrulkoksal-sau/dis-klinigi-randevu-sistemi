using DisKlinigi.Core.Domain.Appointments;
using DisKlinigi.Core.Domain.Users;

namespace DisKlinigi.Core.Domain;

/// <summary>
/// Hata mesajlarında ve ekranda kullanılan Türkçe gün, durum ve rol adları. İşletim sisteminin kültür
/// ayarına bağlı kalmasın diye (macOS/Windows farkı) sabit tablo olarak tutuluyor.
/// </summary>
public static class TurkishNames
{
    public static string Day(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Pazartesi",
        DayOfWeek.Tuesday => "Salı",
        DayOfWeek.Wednesday => "Çarşamba",
        DayOfWeek.Thursday => "Perşembe",
        DayOfWeek.Friday => "Cuma",
        DayOfWeek.Saturday => "Cumartesi",
        DayOfWeek.Sunday => "Pazar",
        _ => throw new ArgumentOutOfRangeException(nameof(day))
    };

    /// <summary>Randevu durumunun ekranda görünen adı (arayüz tasarımındaki rozetlerle aynı).</summary>
    public static string Status(AppointmentStatus status) => status switch
    {
        AppointmentStatus.PendingApproval => "Onay bekliyor",
        AppointmentStatus.Confirmed => "Onaylandı",
        AppointmentStatus.Rejected => "Reddedildi",
        AppointmentStatus.Cancelled => "İptal edildi",
        AppointmentStatus.Completed => "Tamamlandı",
        AppointmentStatus.NoShow => "Gelmedi",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    /// <summary>
    /// Özel ada bulunma eki: "Serdivan Şubesi" → "Serdivan Şubesi'nde", "Kadıköy" → "Kadıköy'de",
    /// "Bağdat" → "Bağdat'ta". Ekranda ve kural mesajlarında "Serdivan Şubesi'ndeki 4 ünit" gibi
    /// cümleler kurmak için.
    /// </summary>
    /// <remarks>
    /// Ünlü uyumu son ünlüye, d/t seçimi son ünsüzün sert olup olmamasına göre yapılıyor.
    /// Ünlüyle biten adlarda kaynaştırma "n"si sadece ad iyelik ekiyle bitiyorsa ("Şubesi",
    /// "Merkezi") geliyor; bunu sondaki "-si/-sı/-su/-sü" ile tahmin ediyorum. Şube adları
    /// "… Şubesi" biçiminde olduğu için bu yeterli. Bilinen sınırı: iyelik eki "s"siz olan adlar
    /// ("Adapazarı" tek başına) "Adapazarı'da" çıkıyor; doğrusu "Adapazarı'nda".
    /// </remarks>
    public static string Locative(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var word = name.Trim();
        var last = char.ToLowerInvariant(word[^1]);
        var lastVowel = word.ToLowerInvariant().LastOrDefault(c => BackVowels.Contains(c) || FrontVowels.Contains(c));
        var vowel = FrontVowels.Contains(lastVowel) ? 'e' : 'a';

        if (FrontVowels.Contains(last) || BackVowels.Contains(last))
        {
            var possessive = word.Length >= 2 && char.ToLowerInvariant(word[^2]) == 's' && "ıiuü".Contains(last);
            return $"{word}'{(possessive ? "n" : "")}d{vowel}";
        }

        var consonant = HardConsonants.Contains(last) ? 't' : 'd';
        return $"{word}'{consonant}{vowel}";
    }

    private const string BackVowels = "aıouâû";
    private const string FrontVowels = "eiöüî";
    private const string HardConsonants = "fstkçşhp";

    private static readonly System.Globalization.CompareInfo TurkishCompare =
        System.Globalization.CultureInfo.GetCultureInfo("tr-TR").CompareInfo;

    /// <summary>
    /// İki ad büyük/küçük harf farkı dışında aynı mı ("Endodonti" ile "endodonti")? Türkçe kurallarıyla
    /// karşılaştırılıyor; " İmplant" ile "implant" aynı, "Işık" ile "isik" farklı. Yönetici panelinde aynı adla
    /// ikinci bölüm, şube ya da hizmet açılmasın diye.
    /// </summary>
    public static bool SameName(string? a, string? b) =>
        TurkishCompare.Compare(a?.Trim(), b?.Trim(), System.Globalization.CompareOptions.IgnoreCase) == 0;

    /// <summary>Rolün menüde görünen adı (arayüz tasarımındaki "Ali Yıldız · Hasta" satırı).</summary>
    public static string Role(UserRole role) => role switch
    {
        UserRole.Patient => "Hasta",
        UserRole.Dentist => "Hekim",
        UserRole.Assistant => "Asistan",
        UserRole.BranchManager => "Şube yöneticisi",
        UserRole.ChainAdministrator => "Zincir yöneticisi",
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };
}
