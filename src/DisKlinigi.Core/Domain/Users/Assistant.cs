using DisKlinigi.Core.Domain.Appointments;

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

    public override Branch? AssignedBranch => Branch;

    public override UserRole Role => UserRole.Assistant;

    public override bool CanManageBranch(Branch branch) => false;

    /// <summary>Asistan bağlı olduğu şubenin onay kuyruğunu yönetiyor; randevunun hangi hekimde olduğu fark etmiyor.</summary>
    public override bool CanApprove(Appointment appointment) => appointment.Branch.IsSameAs(Branch);

    /// <summary>Şubesindeki randevuyu, son 24 saat dahil, iptal edebilir.</summary>
    public override bool CanCancel(Appointment appointment, DateTime now) => appointment.Branch.IsSameAs(Branch);

    public void TransferTo(Branch branch)
    {
        Branch = branch ?? throw new DomainException("Asistanın bağlı olduğu şube belirtilmelidir.");
    }
}
