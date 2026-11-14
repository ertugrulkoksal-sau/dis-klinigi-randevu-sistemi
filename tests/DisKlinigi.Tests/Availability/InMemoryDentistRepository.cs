using DisKlinigi.Core.Abstractions;
using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Users;

namespace DisKlinigi.Tests.Availability;

/// <summary>
/// Müsaitlik servisi testleri için bellek içi hekim repository'si. Kaydedilince Id'si olmayan
/// aralıklara veritabanının yapacağı gibi sıradaki Id'yi veriyor (Id'nin ayarlayıcısı korumalı, yansımayla).
/// </summary>
internal sealed class InMemoryDentistRepository : IDentistRepository
{
    private readonly List<Dentist> _dentists;
    private int _lastWindowId;

    public InMemoryDentistRepository(params Dentist[] dentists)
    {
        _dentists = [.. dentists];
        AssignWindowIds();
    }

    public int SaveCount { get; private set; }

    public Task<Dentist?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_dentists.FirstOrDefault(d => d.Id == id));

    public Task<IReadOnlyList<Dentist>> GetWorkingAtAsync(Branch branch, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Dentist>>(_dentists.Where(d => d.WorksAt(branch)).OrderBy(d => d.FullName).ToList());

    public Task<IReadOnlyList<Dentist>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Dentist>>(_dentists.OrderBy(d => d.FullName).ToList());

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        AssignWindowIds();
        return Task.CompletedTask;
    }

    private void AssignWindowIds()
    {
        var id = typeof(Entity).GetProperty(nameof(Entity.Id))!;
        foreach (var window in _dentists.SelectMany(d => d.Availability).Where(w => w.Id == 0))
            id.SetValue(window, ++_lastWindowId);
    }
}
