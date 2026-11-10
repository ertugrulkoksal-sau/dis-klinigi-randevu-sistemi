using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DisKlinigi.Web.Data.Configurations;

/// <summary>
/// Kullanıcı hiyerarşisi tek tabloda (TPH, K14). Hangi satırın hangi sınıf olduğunu
/// <c>UserType</c> sütunu söylüyor; değerleri <see cref="UserRole"/> adlarıyla aynı.
/// </summary>
/// <remarks>
/// Alt sınıflara özgü sütunlar (hastanın telefonu, hekimin uzmanlığı, asistanın şubesi)
/// diğer satırlarda boş kalıyor. Asistan ve şube yöneticisinin şubesi aynı
/// <c>BranchId</c> sütununu paylaşıyor; ikisinde de anlamı "bağlı olduğu şube".
/// </remarks>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("Users");
        b.HasDiscriminator<string>("UserType")
            .HasValue<Patient>(nameof(UserRole.Patient))
            .HasValue<Dentist>(nameof(UserRole.Dentist))
            .HasValue<Assistant>(nameof(UserRole.Assistant))
            .HasValue<BranchManager>(nameof(UserRole.BranchManager))
            .HasValue<ChainAdministrator>(nameof(UserRole.ChainAdministrator));
        b.Property<string>("UserType").HasMaxLength(30);

        b.Property(x => x.FullName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Email).HasMaxLength(254).IsRequired();
        b.Property(x => x.PasswordHash).HasMaxLength(200).IsRequired();

        // E-posta giriş adı. User.ChangeEmail küçük harfe çevirdiği için aynı adresin büyük
        // harfle ikinci kez kaydedilmesi de bu indekse takılıyor.
        b.HasIndex(x => x.Email).IsUnique();
    }
}

public class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> b)
    {
        b.Property(x => x.PhoneNumber).HasMaxLength(20);
    }
}

public class AssistantConfiguration : IEntityTypeConfiguration<Assistant>
{
    public void Configure(EntityTypeBuilder<Assistant> b)
    {
        b.HasOne(x => x.Branch)
            .WithMany()
            .HasForeignKey("BranchId")
            .HasConstraintName("FK_Users_Branches_BranchId")
            .OnDelete(DeleteBehavior.Restrict);
        SharedBranchColumn(b);
    }

    /// <summary>
    /// EF Core farklı alt sınıflardaki aynı adlı yabancı anahtarları varsayılan olarak ayrı
    /// sütunlara koyuyor (Assistant_BranchId gibi). Asistan ve şube yöneticisi aynı
    /// <c>BranchId</c> sütununu paylaşsın diye sütun adı ve kısıt adı açıkça veriliyor.
    /// </summary>
    internal static void SharedBranchColumn<T>(EntityTypeBuilder<T> b) where T : User =>
        b.Property<int>("BranchId").HasColumnName("BranchId");
}

public class BranchManagerConfiguration : IEntityTypeConfiguration<BranchManager>
{
    public void Configure(EntityTypeBuilder<BranchManager> b)
    {
        b.HasOne(x => x.Branch)
            .WithMany()
            .HasForeignKey("BranchId")
            .HasConstraintName("FK_Users_Branches_BranchId")
            .OnDelete(DeleteBehavior.Restrict);
        AssistantConfiguration.SharedBranchColumn(b);
    }
}

/// <summary>
/// Hekim: uzmanlık, çalıştığı şubeler ve verdiği hizmetler (iki çoka-çok ilişki) ve
/// müsaitlik aralıkları (hekime ait, hekimle birlikte siliniyor).
/// </summary>
public class DentistConfiguration : IEntityTypeConfiguration<Dentist>
{
    public void Configure(EntityTypeBuilder<Dentist> b)
    {
        b.HasOne(x => x.Specialty)
            .WithMany()
            .HasForeignKey("SpecialtyId")
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Branches)
            .WithMany()
            .UsingEntity(
                "DentistBranches",
                r => r.HasOne(typeof(Branch)).WithMany().HasForeignKey("BranchId").OnDelete(DeleteBehavior.Restrict),
                l => l.HasOne(typeof(Dentist)).WithMany().HasForeignKey("DentistId").OnDelete(DeleteBehavior.Cascade));
        b.Navigation(x => x.Branches).HasField("_branches");

        b.HasMany(x => x.Services)
            .WithMany()
            .UsingEntity(
                "DentistServices",
                r => r.HasOne(typeof(Service)).WithMany().HasForeignKey("ServiceId").OnDelete(DeleteBehavior.Restrict),
                l => l.HasOne(typeof(Dentist)).WithMany().HasForeignKey("DentistId").OnDelete(DeleteBehavior.Cascade));
        b.Navigation(x => x.Services).HasField("_services");

        b.HasMany(x => x.Availability)
            .WithOne()
            .HasForeignKey("DentistId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Availability).HasField("_availability");
    }
}

public class AvailabilityWindowConfiguration : IEntityTypeConfiguration<AvailabilityWindow>
{
    public void Configure(EntityTypeBuilder<AvailabilityWindow> b)
    {
        b.ToTable("AvailabilityWindows");
        b.Property(x => x.Day).HasConversion<string>().HasMaxLength(10);
        b.ComplexProperty(x => x.Hours, h =>
        {
            h.Property(x => x.Start).HasColumnName("Starts");
            h.Property(x => x.End).HasColumnName("Ends");
        });

        // Şube silinirken hekimlerin aralıkları sessizce kaybolmasın diye silme engelleniyor;
        // hekim de müsaitliği olan şubeden çıkarılamıyor (K24).
        b.HasOne(x => x.Branch)
            .WithMany()
            .HasForeignKey("BranchId")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
