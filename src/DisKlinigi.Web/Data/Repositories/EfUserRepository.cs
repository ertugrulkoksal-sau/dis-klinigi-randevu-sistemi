using DisKlinigi.Core.Abstractions;
using DisKlinigi.Core.Domain.Users;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DisKlinigi.Web.Data.Repositories;

public class EfUserRepository : IUserRepository
{
    /// <summary>SQLite'ın kısıt ihlali hata kodu (SQLITE_CONSTRAINT); benzersiz indeks de bu kodla dönüyor.</summary>
    private const int SqliteConstraint = 19;

    private readonly AppDbContext _db;

    public EfUserRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        // Veritabanında e-postalar zaten bu biçimde (User.ChangeEmail); sorgu düz eşitlikle indeksi kullanıyor.
        var normalized = User.NormalizeEmail(email);
        return WithBranch(_db.Users).FirstOrDefaultAsync(u => u.Email == normalized, cancellationToken);
    }

    public Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        WithBranch(_db.Users).FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task<bool> TryAddAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        // Olağan durum (e-posta kayıtlı) için hata yakalamaya gerek kalmasın diye önce bakılıyor;
        // aradaki kısa sürede aynı e-postayla gelen ikinci isteği benzersiz indeks yakalıyor.
        if (await _db.Users.AnyAsync(u => u.Email == user.Email, cancellationToken))
            return false;

        _db.Users.Add(user);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: SqliteConstraint })
        {
            // Kaydedilemeyen nesne bağlamda "eklenecek" olarak kalmasın; yoksa sonraki
            // SaveChanges aynı hatayı tekrar verir.
            _db.Entry(user).State = EntityState.Detached;
            return false;
        }
    }

    /// <summary>
    /// Asistan ve şube yöneticisi şubeleriyle yükleniyor; <c>CanApprove</c> ve <c>CanManageBranch</c>
    /// şubeye bakıyor. Hekimin şubeleri ve müsaitliği burada yüklenmiyor; onlar gerektiğinde
    /// <see cref="IDentistRepository"/> hekimi bütün parçalarıyla getiriyor.
    /// </summary>
    private static IQueryable<User> WithBranch(IQueryable<User> query) =>
        query
            .Include(u => ((Assistant)u).Branch)
            .Include(u => ((BranchManager)u).Branch);
}
