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

    public void TransferTo(Branch branch)
    {
        Branch = branch ?? throw new DomainException("Şube yöneticisinin şubesi belirtilmelidir.");
    }
}
