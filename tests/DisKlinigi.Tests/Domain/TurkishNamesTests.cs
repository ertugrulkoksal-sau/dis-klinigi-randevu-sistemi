using DisKlinigi.Core.Domain;

namespace DisKlinigi.Tests.Domain;

public class TurkishNamesTests
{
    [Theory]
    // Örnek verideki şube adları: iyelik ekiyle bittiği için kaynaştırma "n"si geliyor.
    [InlineData("Serdivan Şubesi", "Serdivan Şubesi'nde")]
    [InlineData("Adapazarı Merkez Şubesi", "Adapazarı Merkez Şubesi'nde")]
    [InlineData("Kadıköy Şubesi", "Kadıköy Şubesi'nde")]
    // Ünsüzle biten adlar: ünlü uyumu ve sert ünsüzden sonra "t".
    [InlineData("Kadıköy", "Kadıköy'de")]
    [InlineData("İstanbul", "İstanbul'da")]
    [InlineData("Bağdat", "Bağdat'ta")]
    [InlineData("Kırşehir", "Kırşehir'de")]
    [InlineData("Üsküdar Klinik", "Üsküdar Klinik'te")]
    // Ünlüyle biten ama iyelik ekiyle bitmeyen adlar "n" almıyor.
    [InlineData("Ankara", "Ankara'da")]
    [InlineData("Kocaeli", "Kocaeli'de")]
    [InlineData("  Serdivan Şubesi  ", "Serdivan Şubesi'nde")]
    public void Bulunma_eki_unlu_uyumuna_ve_son_harfe_gore_secilir(string name, string expected)
    {
        Assert.Equal(expected, TurkishNames.Locative(name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Bos_ada_ek_getirilemez(string name)
    {
        Assert.Throws<ArgumentException>(() => TurkishNames.Locative(name));
    }
}
