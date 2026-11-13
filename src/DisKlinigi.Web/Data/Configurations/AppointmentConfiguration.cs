using DisKlinigi.Core.Domain.Appointments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DisKlinigi.Web.Data.Configurations;

/// <summary>
/// Randevu. Zaman aralığı (<c>TimeRange</c>) ayrı bir tablo değil, randevu satırındaki
/// <c>Start</c> ve <c>End</c> sütunları (karmaşık tip, K37).
/// </summary>
public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> b)
    {
        b.ToTable("Appointments");

        b.ComplexProperty(x => x.Period, p =>
        {
            p.Property(x => x.Start).HasColumnName("Start");
            p.Property(x => x.End).HasColumnName("End");
        });

        // Durum metin olarak: veritabanına bakınca "Confirmed" okunuyor ve enum'daki sıra
        // değişse bile eski kayıtların anlamı kaymıyor (K26, K38).
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

        b.Property(x => x.ServiceNameAtBooking).HasMaxLength(100).IsRequired();
        b.Property(x => x.PriceAtBooking).HasPrecision(10, 2);
        b.Property(x => x.RejectionReason).HasMaxLength(Appointment.MaxRejectionReasonLength);
        b.Property(x => x.PatientNote).HasMaxLength(Appointment.MaxPatientNoteLength);

        // Randevu geçmişi kayıt olarak değerli (alınan ücret, gelmeyen hasta). Hasta, hekim,
        // hizmet ya da şube silinirken randevuları da silinmesin diye hepsi Restrict.
        b.HasOne(x => x.Patient).WithMany().HasForeignKey("PatientId").OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Dentist).WithMany().HasForeignKey("DentistId").OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Service).WithMany().HasForeignKey("ServiceId").OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Branch).WithMany().HasForeignKey("BranchId").OnDelete(DeleteBehavior.Restrict);
    }
}
