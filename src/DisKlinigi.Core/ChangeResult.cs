namespace DisKlinigi.Core;

/// <summary>
/// Bir kaydı değiştiren işlemin (müsaitlik aralığı, şube ayarları, katalog, personel) sonucu; reddedildiyse
/// ekranda gösterilecek neden.
/// </summary>
/// <remarks>
/// 13 Kasım'da müsaitlik servisi için <c>AvailabilityChangeResult</c> adıyla yazılmıştı. Yönetici panelindeki
/// servisler de aynı üç cevabı veriyor (yapıldı, bulunamadı, reddedildi); ikinci bir kopyasını açmak yerine
/// adını genelleştirip buraya taşıdım. "Bulunamadı" ile "yetkin yok" bilerek ayrılmıyor (K46): şube
/// yöneticisi başka şubenin numarasını denerse şubenin var olduğu anlaşılmıyor.
/// </remarks>
public sealed record ChangeResult(bool Succeeded, bool NotFound, string? Error)
{
    public static readonly ChangeResult Done = new(true, false, null);
    public static readonly ChangeResult Missing = new(false, true, null);

    public static ChangeResult Refused(string error) => new(false, false, error);
}

/// <summary>Yeni kayıt açan işlemin sonucu: açılan kayıt ya da reddedilme nedeni.</summary>
public sealed record CreateResult<T>(T? Created, string? Error) where T : class
{
    public bool Succeeded => Created is not null;

    public static CreateResult<T> Done(T created) => new(created, null);

    public static CreateResult<T> Refused(string error) => new(null, error);
}
