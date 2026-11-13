using System.ComponentModel.DataAnnotations;
using DisKlinigi.Core.Appointments;
using DisKlinigi.Core.Availability;
using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Appointments;
using DisKlinigi.Core.Domain.Users;

namespace DisKlinigi.Web.Models;

/// <summary>Günlük takvim (ekran 4a). Hekim kendi gününü, asistan şubesindeki bir hekimin gününü görüyor.</summary>
public sealed class CalendarViewModel
{
    public required User Viewer { get; init; }

    /// <summary>Butonların ve "şu an" çizgisinin hesaplandığı an.</summary>
    public required DateTime Now { get; init; }
    public required DateOnly Date { get; init; }

    /// <summary>Takvimi gösterilen hekim; asistanın şubesinde hiç hekim yoksa <c>null</c>.</summary>
    public Dentist? Dentist { get; init; }
    public DaySchedule? Day { get; init; }

    /// <summary>Asistanın hekim seçicisi; hekimin kendi takviminde boş.</summary>
    public IReadOnlyList<Dentist> DentistChoices { get; init; } = [];

    /// <summary>Ret gerekçesi formu açık olan randevu ("Reddet…" tıklanınca).</summary>
    public int? RejectId { get; init; }

    /// <summary>İptal onayı gösterilen randevu ("İptal et…" tıklanınca).</summary>
    public int? CancelId { get; init; }

    public bool IsToday => Date == DateOnly.FromDateTime(Now);

    /// <summary>Bu randevuda çıkacak butonlar: durum + kişinin yetkisi (<see cref="Appointment.ActionsFor"/>).</summary>
    public AppointmentActions ActionsFor(Appointment appointment) => appointment.ActionsFor(Viewer, Now);
}

/// <summary>Onay kuyruğu (ekran 4b).</summary>
public sealed class QueueViewModel
{
    public required User Viewer { get; init; }
    public required DateTime Now { get; init; }

    /// <summary>Kullanıcının onaylayabileceği bekleyen randevular, filtre uygulanmış, en yakın tarih önce.</summary>
    public required IReadOnlyList<Appointment> Items { get; init; }

    /// <summary>Asistanın hekim filtresi (şubedeki hekimler); hekimin kendi kuyruğunda boş.</summary>
    public IReadOnlyList<Dentist> DentistChoices { get; init; } = [];
    public int? DentistId { get; init; }
    public int? RejectId { get; init; }

    public AppointmentActions ActionsFor(Appointment appointment) => appointment.ActionsFor(Viewer, Now);
}

/// <summary>Müsaitlik aralıkları (ekran 4c).</summary>
public sealed class AvailabilityViewModel
{
    public required Dentist Dentist { get; init; }
    public required IReadOnlyList<WindowUsage> Windows { get; init; }

    /// <summary>Silme onayı gösterilen aralık ("Sil…" tıklanınca).</summary>
    public int? DeleteId { get; init; }

    public AvailabilityForm Form { get; init; } = new();

    /// <summary>Ekleme kurallara takıldıysa alan sınıfının mesajı.</summary>
    public string? Error { get; init; }

    /// <summary>Formdaki şube listesi: hekimin çalıştığı şubeler.</summary>
    public IEnumerable<Branch> Branches => Dentist.Branches.OrderBy(b => b.Name);
}

/// <summary>"Aralık ekle" formu. Saatler 15 dakikalık adımlarla seçiliyor (boş saat ızgarasıyla aynı, K32).</summary>
public sealed class AvailabilityForm
{
    [Display(Name = "Şube")]
    public int BranchId { get; set; }

    [Display(Name = "Gün")]
    public DayOfWeek Day { get; set; } = DayOfWeek.Monday;

    [Display(Name = "Başlangıç")]
    [Required(ErrorMessage = "Başlangıç saatini girin.")]
    public TimeOnly? Start { get; set; }

    [Display(Name = "Bitiş")]
    [Required(ErrorMessage = "Bitiş saatini girin.")]
    public TimeOnly? End { get; set; }
}
