using System.Diagnostics.CodeAnalysis;
using DisKlinigi.Core.Domain.Appointments;

namespace DisKlinigi.Core.Appointments;

/// <summary>Bir randevu işleminin (iptal; 13 Kasım'dan sonra onay, ret, sonuç) üç olası cevabı.</summary>
public enum AppointmentActionOutcome
{
    Succeeded,

    /// <summary>Randevu yok ya da kullanıcının bu randevuyla ilgisi yok; ikisi bilerek ayırt edilmiyor.</summary>
    NotFound,

    /// <summary>Randevu var ama işlem şu an yapılamıyor (durum, zaman ya da yetki kuralı).</summary>
    Refused
}

/// <summary>
/// Randevu işleminin sonucu. Reddedilen işlem olağan bir cevap (24 saat kuralı gibi), bu yüzden
/// controller'a istisna değil sonuç nesnesi dönüyor; mesaj alan sınıfının hata mesajı.
/// </summary>
public sealed class AppointmentActionResult
{
    private AppointmentActionResult(AppointmentActionOutcome outcome, Appointment? appointment, string? error)
    {
        Outcome = outcome;
        Appointment = appointment;
        Error = error;
    }

    public AppointmentActionOutcome Outcome { get; }

    /// <summary>İşlem yapılan randevu; bulunamadıysa <c>null</c>.</summary>
    public Appointment? Appointment { get; }

    /// <summary>İşlem reddedildiyse kullanıcıya gösterilecek neden.</summary>
    public string? Error { get; }

    [MemberNotNullWhen(true, nameof(Appointment))]
    public bool Succeeded => Outcome == AppointmentActionOutcome.Succeeded;

    public static readonly AppointmentActionResult NotFound = new(AppointmentActionOutcome.NotFound, null, null);

    internal static AppointmentActionResult Done(Appointment appointment) =>
        new(AppointmentActionOutcome.Succeeded, appointment, null);

    internal static AppointmentActionResult Refused(Appointment appointment, string error) =>
        new(AppointmentActionOutcome.Refused, appointment, error);
}
