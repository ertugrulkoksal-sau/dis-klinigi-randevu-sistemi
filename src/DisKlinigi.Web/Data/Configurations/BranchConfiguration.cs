using DisKlinigi.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DisKlinigi.Web.Data.Configurations;

/// <summary>Şube, haftalık çalışma saatleri ve barındırdığı bölümler.</summary>
public class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> b)
    {
        b.ToTable("Branches");
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.City).HasMaxLength(50).IsRequired();
        b.Property(x => x.Address).HasMaxLength(250);
        b.HasIndex(x => x.Name).IsUnique();

        // Çalışma saatleri şubenin parçası; şube silinirse saatleri de siliniyor.
        b.HasMany(x => x.WorkingHours)
            .WithOne()
            .HasForeignKey("BranchId")
            .OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.WorkingHours).HasField("_workingHours");

        // Şube ↔ bölüm çoka-çok. Bölüm tarafında geri gezinme yok, çünkü alan modeli
        // "bu bölüm hangi şubelerde var?" sorusunu hiç sormuyor.
        b.HasMany(x => x.Departments)
            .WithMany()
            .UsingEntity(
                "BranchDepartments",
                r => r.HasOne(typeof(Department)).WithMany().HasForeignKey("DepartmentId").OnDelete(DeleteBehavior.Restrict),
                l => l.HasOne(typeof(Branch)).WithMany().HasForeignKey("BranchId").OnDelete(DeleteBehavior.Cascade));
        b.Navigation(x => x.Departments).HasField("_departments");
    }
}

/// <summary>
/// Şubenin bir günlük çalışma saati. Kendi Id'si yok; anahtarı (şube, gün). Bu anahtar
/// <see cref="Branch.SetWorkingHours"/>'taki "aynı güne iki kayıt olmaz" kuralını
/// veritabanında da garanti ediyor (K37).
/// </summary>
public class DailyWorkingHoursConfiguration : IEntityTypeConfiguration<DailyWorkingHours>
{
    public void Configure(EntityTypeBuilder<DailyWorkingHours> b)
    {
        b.ToTable("BranchWorkingHours");
        b.Property<int>("BranchId");
        b.HasKey("BranchId", nameof(DailyWorkingHours.Day));
        b.Property(x => x.Day).HasConversion<string>().HasMaxLength(10);
        b.ComplexProperty(x => x.Hours, h =>
        {
            h.Property(x => x.Start).HasColumnName("Opens");
            h.Property(x => x.End).HasColumnName("Closes");
        });
    }
}
