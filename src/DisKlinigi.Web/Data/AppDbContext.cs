using DisKlinigi.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace DisKlinigi.Web.Data;

/// <summary>
/// EF Core veritabanı bağlamı. Tablo eşlemeleri Fluent API ile burada yapılıyor;
/// böylece Core'daki alan sınıfları EF Core'a ait özniteliklerle kirlenmiyor.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Branch> Branches => Set<Branch>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Branch>(b =>
        {
            b.ToTable("Branches");
            b.Property(x => x.Name).HasMaxLength(100).IsRequired();
            b.Property(x => x.City).HasMaxLength(50).IsRequired();
            b.Property(x => x.Address).HasMaxLength(250);
            b.HasIndex(x => x.Name).IsUnique();

            // Çalışma saatleri ve bölümler alan modelinde var ama tablolara eşlenmeleri
            // veritabanı adımında (10 Kasım) yapılacak; o zamana kadar EF Core bunları görmüyor.
            b.Ignore(x => x.WorkingHours);
            b.Ignore(x => x.Departments);
        });
    }
}
