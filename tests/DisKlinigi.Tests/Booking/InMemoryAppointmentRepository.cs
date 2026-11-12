using DisKlinigi.Core.Abstractions;
using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Appointments;
using DisKlinigi.Core.Domain.Users;

namespace DisKlinigi.Tests.Booking;

/// <summary>
/// Planlayıcı testleri için bellek içi repository.
/// </summary>
/// <remarks>
/// İşlem, SQLite'taki <c>BEGIN IMMEDIATE</c> davranışını taklit ediyor (K19): işlem açılırken
/// yazma kilidi alınıyor, ikinci işlem ilki kapanana kadar bekliyor. Eklenen randevu
/// sadece onaylanınca (commit) listeye giriyor; onaylanmadan kapatılan işlem iz bırakmıyor.
/// Okuma bilerek <see cref="Task.Yield"/> ile bekletiliyor ki eş zamanlı testlerde iki istek
/// gerçekten araya girebilsin; planlayıcı okumayı işlemin dışında yapsaydı test yakalardı.
/// </remarks>
internal sealed class InMemoryAppointmentRepository : IAppointmentRepository
{
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly List<Appointment> _saved = [];
    private readonly List<Appointment> _staged = [];

    private int _lastId;

    public InMemoryAppointmentRepository(params Appointment[] existing)
    {
        foreach (var appointment in existing)
            Store(appointment);
    }

    public IReadOnlyList<Appointment> Saved => _saved.AsReadOnly();

    public int CommitCount { get; private set; }

    /// <summary>İptal gibi durum değişikliklerinin kaç kez kaydedildiği.</summary>
    public int SaveCount { get; private set; }

    public Task<Appointment?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        lock (_saved)
            return Task.FromResult(_saved.FirstOrDefault(a => a.Id == id));
    }

    public Task<IReadOnlyList<Appointment>> GetForPatientAsync(Patient patient, CancellationToken cancellationToken = default)
    {
        lock (_saved)
        {
            IReadOnlyList<Appointment> result = _saved
                .Where(a => a.Patient.IsSameAs(patient))
                .OrderBy(a => a.Period.Start)
                .ToList();
            return Task.FromResult(result);
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Kaydedilen randevuya veritabanının yapacağı gibi sıradaki Id'yi verir. Id'nin ayarlayıcısı
    /// alan sınıfında korumalı (Id'yi veritabanı veriyor); testte bunu yansıma ile taklit ediyorum.
    /// </summary>
    private void Store(Appointment appointment)
    {
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(appointment, ++_lastId);
        _saved.Add(appointment);
    }

    public async Task<IReadOnlyList<Appointment>> GetActiveOnDateAsync(
        Dentist dentist, Patient patient, Branch branch, DateOnly date, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        var day = new TimeRange(date.ToDateTime(TimeOnly.MinValue), date.AddDays(1).ToDateTime(TimeOnly.MinValue));
        lock (_saved)
        {
            return _saved
                .Where(a => a.IsActive && a.Period.Overlaps(day))
                .Where(a => a.Dentist.IsSameAs(dentist) || a.Patient.IsSameAs(patient) || a.Branch.IsSameAs(branch))
                .ToList();
        }
    }

    public void Add(Appointment appointment) => _staged.Add(appointment);

    public async Task<IBookingTransaction> BeginBookingTransactionAsync(CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        return new Transaction(this);
    }

    private sealed class Transaction(InMemoryAppointmentRepository owner) : IBookingTransaction
    {
        private bool _closed;

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            lock (owner._saved)
            {
                foreach (var appointment in owner._staged)
                    owner.Store(appointment);
            }
            owner._staged.Clear();
            owner.CommitCount++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            if (!_closed)
            {
                _closed = true;
                owner._staged.Clear();
                owner._writeLock.Release();
            }
            return ValueTask.CompletedTask;
        }
    }
}
