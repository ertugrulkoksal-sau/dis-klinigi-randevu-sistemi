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

    public override UserRole Role => UserRole.BranchManager;

    /// <summary>Yetki sınırı burada: başka bir şube sorulursa cevap hayır.</summary>
    public override bool CanManageBranch(Branch branch) => Branch.IsSameAs(branch);

    /// <summary>
    /// Yönetici randevu onaylamıyor: onay, hastanın ve sürenin tıbbi olarak
    /// değerlendirilmesi demek (K2); bu hekimin ya da sağlık personelinin işi (K27).
    /// </summary>
    public override bool CanApprove(Appointment appointment) => false;

    /// <summary>Kendi şubesindeki randevuyu iptal edebilir (ör. şube o gün kapanırsa).</summary>
    public override bool CanCancel(Appointment appointment, DateTime now) => CanManageBranch(appointment.Branch);

    public void TransferTo(Branch branch)
    {
        Branch = branch ?? throw new DomainException("Şube yöneticisinin şubesi belirtilmelidir.");
    }
}
