using DisKlinigi.Core.Domain.Appointments;

namespace DisKlinigi.Core.Domain.Users;

/// <summary>
/// Şube yöneticisi: sadece kendi şubesini yönetebiliyor (tasarım kararı K3).
/// </summary>
public class BranchManager : User
{
    public Branch Branch { get; private set; } = null!;

    private BranchManager()
    {
    }

    public BranchManager(string fullName, string email, string passwordHash, Branch branch)
        : base(fullName, email, passwordHash)
    {
        TransferTo(branch);
    }

    public override Branch? AssignedBranch => Branch;

    public override UserRole Role => UserRole.BranchManager;

    /// <summary>Yetki sınırı burada: başka bir şube sorulursa cevap hayır.</summary>
    public override bool CanManageBranch(Branch branch) => Branch.IsSameAs(branch);

    /// <summary>
    /// Yönetici randevu onaylamıyor: onay, hastanın ve sürenin tıbbi olarak
    /// değerlendirilmesi demek (K2); bu hekimin ya da sağlık personelinin işi (K27).
    /// </summary>
    public override bool CanApprove(Appointment appointment) => false;

    /// <summary>
    /// Yönetici randevu iptal etmiyor (K34). Şubenin ayarlarını yönetiyor; randevu işlemleri
    /// randevunun hekiminde ve şubenin asistanında. İlk halinde kendi şubesindeki randevuyu iptal
    /// edebiliyordu ama bunun için ayrı bir ekran gerekecekti, kapsamı büyütmemek için kaldırdım.
    /// </summary>
    public override bool CanCancel(Appointment appointment, DateTime now) => false;

    public void TransferTo(Branch branch)
    {
        Branch = branch ?? throw new DomainException("Şube yöneticisinin şubesi belirtilmelidir.");
    }
}
