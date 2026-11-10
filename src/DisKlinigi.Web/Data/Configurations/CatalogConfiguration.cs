using DisKlinigi.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DisKlinigi.Web.Data.Configurations;

/// <summary>Zincir genelindeki bölüm kataloğu.</summary>
public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> b)
    {
        b.ToTable("Departments");
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();
    }
}

/// <summary>Hizmetler: süre dakika olarak, ücret ondalık sayı olarak saklanıyor.</summary>
public class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> b)
    {
        b.ToTable("Services");
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();

        // SQLite'ta TimeSpan metin olarak saklanıyor ("01:00:00"). Süre hep 5 dakikanın
        // katı olduğu için (K25) tam sayı dakika hem okunur hem de sorguda kullanılabilir.
        b.Property(x => x.Duration)
            .HasColumnName("DurationMinutes")
            .HasConversion(v => (int)v.TotalMinutes, v => TimeSpan.FromMinutes(v));

        // SQLite'ın ondalık tipi yok; EF Core decimal'i metin olarak saklıyor, böylece
        // 2500.00 gibi değerler kayan noktaya çevrilip yuvarlanmıyor (K38).
        b.Property(x => x.Price).HasPrecision(10, 2);

        b.HasOne(x => x.Department)
            .WithMany()
            .HasForeignKey("DepartmentId")
            .OnDelete(DeleteBehavior.Restrict);

        // Aynı bölümde aynı adla iki hizmet olmasın.
        b.HasIndex("DepartmentId", nameof(Service.Name)).IsUnique();
    }
}
