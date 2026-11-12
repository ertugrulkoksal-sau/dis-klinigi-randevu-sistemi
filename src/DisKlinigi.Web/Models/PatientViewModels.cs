using System.ComponentModel.DataAnnotations;
using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Appointments;
using DisKlinigi.Core.Domain.Users;

namespace DisKlinigi.Web.Models;

/// <summary>
/// Randevu al, 1–4. adımlar (ekran 2a): şube → bölüm → hizmet → hekim. Her seçim bir sonraki
/// listeyi daraltıyor; seçimler sorgu parametrelerinde, sayfa JavaScript'siz çalışıyor.
/// </summary>
public sealed class BookingChoicesViewModel
{
    public required IReadOnlyList<Branch> Branches { get; init; }
    public Branch? Branch { get; init; }

    /// <summary>Seçilen şubenin barındırdığı bölümler.</summary>
    public IReadOnlyList<Department> Departments { get; init; } = [];
    public Department? Department { get; init; }

    /// <summary>Seçilen bölümün hizmetleri.</summary>
    public IReadOnlyList<Service> Services { get; init; } = [];
    public Service? Service { get; init; }

    /// <summary>Bu şubede bu hizmet için randevu verebilen hekimler (<see cref="Dentist.CanServe"/>).</summary>
    public IReadOnlyList<Dentist> Dentists { get; init; } = [];
    public Dentist? Dentist { get; init; }

    /// <summary>Şu an seçilmesi beklenen adım (1–4); dördü de seçildiyse 4.</summary>
    public int CurrentStep => Branch is null ? 1 : Department is null ? 2 : Service is null ? 3 : 4;
}

/// <summary>
/// Randevu al, 5–6. adımlar (ekran 2b): gün, boş saat, özet ve onay. Kayıt kurallara takılırsa
/// aynı sayfa çiğnenen kurallarla ve seçimi kaldırılmış olarak yeniden açılıyor.
/// </summary>
public sealed class BookingScheduleViewModel
{
    /// <summary>Gün listesi bu kadar günlük pencerelerle ilerliyor ("Sonraki 2 hafta").</summary>
    public const int DaysPerPage = 14;

    public required Branch Branch { get; init; }
    public required Service Service { get; init; }
    public required Dentist Dentist { get; init; }

    /// <summary>Gün penceresinin ilk günü; bugünden önce olamaz.</summary>
    public required DateOnly From { get; init; }
    public required DateOnly Today { get; init; }

    /// <summary>Penceredeki, hekimin bu şubede çalıştığı günler.</summary>
    public required IReadOnlyList<DateOnly> Days { get; init; }
    public DateOnly? Date { get; init; }

    /// <summary>Seçilen günün kurallardan geçen boş saatleri (planlayıcının <c>GetFreeSlotsAsync</c>'i).</summary>
    public IReadOnlyList<TimeRange> Slots { get; init; } = [];
    public TimeRange? Selected { get; init; }

    public BookingForm Form { get; init; } = new();

    /// <summary>Kaydederken çiğnenen kurallar; boşsa hata kutusu çıkmıyor.</summary>
    public IReadOnlyList<string> Violations { get; init; } = [];

    public bool HasPreviousPage => From > Today;
}

/// <summary>"Randevuyu oluştur" formu. Seçimler gizli alanlarda; saat ve tarih kültürden bağımsız yazılıyor.</summary>
public sealed class BookingForm
{
    public int BranchId { get; set; }
    public int ServiceId { get; set; }
    public int DentistId { get; set; }

    [Required(ErrorMessage = "Listeden bir gün seçin.")]
    public DateOnly? Date { get; set; }

    [Required(ErrorMessage = "Listeden bir saat seçin.")]
    public TimeOnly? Start { get; set; }

    /// <summary>Sınır alan sınıfıyla aynı sabit (K35); ekrandaki kontrol atlansa da randevu isteği reddediyor.</summary>
    [Display(Name = "Hekime not")]
    [StringLength(Appointment.MaxPatientNoteLength, ErrorMessage = "Hekime not en fazla {1} karakter olabilir.")]
    public string? Note { get; set; }
}

/// <summary>Randevularım (ekran 3): yaklaşanlar kart olarak, geçmiş ve sonuçlananlar tablo olarak.</summary>
public sealed class PatientAppointmentsViewModel
{
    public required Patient Patient { get; init; }

    /// <summary>Butonların ve rozetlerin hesaplandığı an.</summary>
    public required DateTime Now { get; init; }

    /// <summary>Saati tutan ve başlamamış randevular, en yakını önce.</summary>
    public required IReadOnlyList<Appointment> Upcoming { get; init; }

    /// <summary>Geri kalanlar (sonuçlanan, iptal/ret, saati gelmiş), en yenisi önce.</summary>
    public required IReadOnlyList<Appointment> Past { get; init; }

    /// <summary>İptal onayı gösterilen randevu ("İptal et…" tıklanınca).</summary>
    public int? ConfirmCancelId { get; init; }

    /// <summary>Hasta bu randevuyu şimdi iptal edebilir mi? Cevap hasta sınıfında (24 saat kuralı, K4).</summary>
    public bool CanCancel(Appointment appointment) => Patient.CanCancel(appointment, Now);
}
