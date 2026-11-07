namespace DisKlinigi.Core.Domain.Users;

/// <summary>
/// Sağlık personeli (asistan). Tek bir şubeye bağlı; o şubenin onay kuyruğunu
/// yönetiyor ama şubenin ayarlarını (saat, bölüm, personel) değiştiremiyor.
/// </summary>
public class Assistant : User
{
    public Branch Branch { get; private set; } = null!;

    private Assistant()
    {
    }

    public Assistant(string fullName, string email, string passwordHash, Branch branch)
        : base(fullName, email, passwordHash)
    {
        TransferTo(branch);
    }

    public override UserRole Role => UserRole.Assistant;

    public override bool CanManageBranch(Branch branch) => false;

    public void TransferTo(Branch branch)
    {
        Branch = branch ?? throw new DomainException("Asistanın bağlı olduğu şube belirtilmelidir.");
    }
}
