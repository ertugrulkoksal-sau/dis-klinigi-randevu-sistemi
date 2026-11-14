using DisKlinigi.Core.Abstractions;
using DisKlinigi.Core.Domain.Users;

namespace DisKlinigi.Tests.Accounts;

/// <summary>Hesap servisi testleri için bellek içi kullanıcı listesi; e-posta benzersizliğini veritabanı gibi koruyor.</summary>
internal sealed class InMemoryUserRepository : IUserRepository
{
    private readonly List<User> _users = [];

    public IReadOnlyList<User> Users => _users;

    public InMemoryUserRepository(params User[] users)
    {
        _users.AddRange(users);
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = User.NormalizeEmail(email);
        return Task.FromResult(_users.FirstOrDefault(u => u.Email == normalized));
    }

    public Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.FirstOrDefault(u => u.Id == id));

    public Task<IReadOnlyList<Assistant>> GetAssistantsAtAsync(DisKlinigi.Core.Domain.Branch branch, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Assistant>>(_users.OfType<Assistant>().Where(a => a.Branch.IsSameAs(branch)).OrderBy(a => a.FullName).ToList());

    public Task<bool> TryAddAsync(User user, CancellationToken cancellationToken = default)
    {
        if (_users.Any(u => u.Email == user.Email))
            return Task.FromResult(false);
        _users.Add(user);
        return Task.FromResult(true);
    }
}
