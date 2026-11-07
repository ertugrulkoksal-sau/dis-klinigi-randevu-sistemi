namespace DisKlinigi.Core.Domain.Users;

/// <summary>
/// Zincir yöneticisi: şube açıp kapatabiliyor, hizmet kataloğunu yönetiyor ve
/// her şubede şube yöneticisinin yapabildiği her şeyi yapabiliyor.
/// Tek klinikli kurumda tek şubeyi yöneten kişi de bu rolde.
/// </summary>
public class ChainAdministrator : User
{
    private ChainAdministrator()
    {
    }

    public ChainAdministrator(string fullName, string email, string passwordHash)
        : base(fullName, email, passwordHash)
    {
    }

    public override UserRole Role => UserRole.ChainAdministrator;

    public override bool CanManageBranch(Branch branch) => branch is not null;

    public override bool CanManageCatalog => true;
}
