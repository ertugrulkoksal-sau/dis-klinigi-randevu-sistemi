namespace DisKlinigi.Core.Domain;

/// <summary>
/// Veritabanında kimliği (Id) olan tüm alan sınıflarının ortak tabanı.
/// </summary>
/// <remarks>
/// İki nesnenin "aynı kayıt" olup olmadığını <see cref="IsSameAs"/> ile soruyorum.
/// Henüz kaydedilmemiş nesnelerin Id'si 0 olduğu için sadece Id'ye bakmak yanlış
/// sonuç verirdi (kaydedilmemiş iki farklı şube "aynı" görünürdü); sadece referansa
/// bakmak da veritabanından iki ayrı sorguyla gelen aynı satırı "farklı" sayardı.
/// Bu yüzden önce referansa, Id atanmışsa Id'ye bakılıyor.
/// Equals/GetHashCode'u ezmedim: Id kayıttan sonra değiştiği için hash değeri de
/// değişirdi ve nesne bir HashSet içindeyse kaybolurdu.
/// </remarks>
public abstract class Entity
{
    public int Id { get; protected set; }

    public bool IsSameAs(Entity? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        return Id != 0 && Id == other.Id && other.GetType() == GetType();
    }
}
