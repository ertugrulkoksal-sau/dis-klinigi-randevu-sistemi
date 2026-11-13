using DisKlinigi.Core.Abstractions;
using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Appointments;
using DisKlinigi.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DisKlinigi.Web.Data.Repositories;

/// <summary>
/// Randevu planlayıcısının ve randevu işlemlerinin ihtiyaç duyduğu sorgular, randevu alma işlemi (K17, K19).
/// </summary>
public class EfAppointmentRepository : IAppointmentRepository
{
    private readonly AppDbContext _db;

    public EfAppointmentRepository(AppDbContext db)
    {
        _db = db;
    }

    /// <remarks>
    /// Tek sorgu: o günle kesişen, durumu aktif olan ve hekime, hastaya ya da şubeye ait
    /// randevular. Kurallar diğer randevunun hekimine, hastasına ve şubesine baktığı için
    /// (<c>IsSameAs</c>, mesajlardaki şube adı) bu üçü birlikte yükleniyor. Aralık yarı açık:
    /// önceki gün 23:00'te biten randevu bu güne girmiyor.
    /// </remarks>
    public async Task<IReadOnlyList<Appointment>> GetActiveOnDateAsync(
        Dentist dentist, Patient patient, Branch branch, DateOnly date, CancellationToken cancellationToken = default)
    {
        var dayStart = date.ToDateTime(TimeOnly.MinValue);
        var dayEnd = dayStart.AddDays(1);
        var active = AppointmentState.ActiveStatuses;
        int dentistId = dentist.Id, patientId = patient.Id, branchId = branch.Id;

        return await _db.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Dentist)
            .Include(a => a.Branch)
            .Where(a => active.Contains(a.Status))
            .Where(a => a.Period.Start < dayEnd && a.Period.End > dayStart)
            .Where(a => a.Dentist.Id == dentistId || a.Patient.Id == patientId || a.Branch.Id == branchId)
            .ToListAsync(cancellationToken);
    }

    public Task<Appointment?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _db.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Dentist)
            .Include(a => a.Branch)
            .Include(a => a.Service)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    /// <remarks>
    /// Liste ekranı için izlenmiyor (AsNoTracking). Ekranda hizmetin o anki adı değil randevudaki
    /// kopyası (K9) gösterildiği için hizmet yüklenmiyor; hasta zaten bilinen kişi.
    /// </remarks>
    public async Task<IReadOnlyList<Appointment>> GetForPatientAsync(Patient patient, CancellationToken cancellationToken = default)
    {
        var patientId = patient.Id;
        return await _db.Appointments
            .AsNoTracking()
            .Include(a => a.Patient)
            .Include(a => a.Dentist)
            .Include(a => a.Branch)
            .Where(a => a.Patient.Id == patientId)
            .OrderBy(a => a.Period.Start)
            .ToListAsync(cancellationToken);
    }

    /// <remarks>İzlenmiyor; kuyrukta işlem yapılınca randevu <see cref="GetByIdAsync"/> ile yeniden yükleniyor.</remarks>
    public async Task<IReadOnlyList<Appointment>> GetPendingAsync(CancellationToken cancellationToken = default) =>
        await WithParties(_db.Appointments.AsNoTracking())
            .Where(a => a.Status == AppointmentStatus.PendingApproval)
            .OrderBy(a => a.Period.Start)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Appointment>> GetForDentistOnDateAsync(
        Dentist dentist, DateOnly date, CancellationToken cancellationToken = default)
    {
        var dayStart = date.ToDateTime(TimeOnly.MinValue);
        var dayEnd = dayStart.AddDays(1);
        var dentistId = dentist.Id;
        return await WithParties(_db.Appointments.AsNoTracking())
            .Where(a => a.Dentist.Id == dentistId)
            .Where(a => a.Period.Start < dayEnd && a.Period.End > dayStart)
            .OrderBy(a => a.Period.Start)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Appointment>> GetUpcomingForDentistAsync(
        Dentist dentist, DateTime from, CancellationToken cancellationToken = default)
    {
        var active = AppointmentState.ActiveStatuses;
        var dentistId = dentist.Id;
        return await WithParties(_db.Appointments.AsNoTracking())
            .Where(a => a.Dentist.Id == dentistId && active.Contains(a.Status) && a.Period.Start > from)
            .OrderBy(a => a.Period.Start)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Liste ekranlarının ve yetki sorularının baktığı üç taraf: hasta, hekim, şube.</summary>
    private static IQueryable<Appointment> WithParties(IQueryable<Appointment> query) =>
        query.Include(a => a.Patient).Include(a => a.Dentist).Include(a => a.Branch);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => _db.SaveChangesAsync(cancellationToken);

    public void Add(Appointment appointment)
    {
        ArgumentNullException.ThrowIfNull(appointment);
        _db.Appointments.Add(appointment);
    }

    /// <remarks>
    /// SQLite'ta <c>BeginTransactionAsync</c> işlemi <c>BEGIN IMMEDIATE</c> ile açıyor:
    /// yazma kilidi okuma yapılmadan önce alınıyor. Aynı anda gelen ikinci istek burada
    /// bekliyor (Microsoft.Data.Sqlite kilit açılana kadar komut zaman aşımı süresince
    /// tekrar deniyor), ilk işlem bitince kontrolünü güncel veriyle yapıyor.
    /// Bunun gerçekten böyle çalıştığını <c>EfAppointmentRepositoryTests</c> iki ayrı
    /// bağlantıyla sınıyor.
    /// </remarks>
    public async Task<IBookingTransaction> BeginBookingTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        return new EfBookingTransaction(_db, transaction);
    }

    private sealed class EfBookingTransaction(AppDbContext db, IDbContextTransaction transaction) : IBookingTransaction
    {
        private bool _committed;

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            _committed = true;
        }

        /// <summary>
        /// Onaylanmadan kapatılan işlem geri alınıyor. Eklenmiş ama kaydedilmemiş randevular
        /// da bağlamdan çıkarılıyor; yoksa aynı istekte sonraki bir <c>SaveChanges</c>
        /// kuralları geçmemiş bir randevuyu kaydedebilirdi.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (!_committed)
            {
                foreach (var entry in db.ChangeTracker.Entries<Appointment>().Where(e => e.State == EntityState.Added).ToList())
                    entry.State = EntityState.Detached;
            }
            await transaction.DisposeAsync();
        }
    }
}
