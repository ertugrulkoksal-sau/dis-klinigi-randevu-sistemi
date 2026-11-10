using DisKlinigi.Web.Security;

namespace DisKlinigi.Tests.Data;

/// <summary>Parola özetleyici: düz metin saklanmıyor, tuz her seferinde farklı (K15).</summary>
public class AspNetPasswordHasherTests
{
    private readonly AspNetPasswordHasher _hasher = new();

    [Fact]
    public void Ozet_parolanin_kendisini_icermez_ve_dogru_parolayla_dogrulanir()
    {
        var hash = _hasher.Hash("Gizli-Parola-42");

        Assert.DoesNotContain("Gizli-Parola-42", hash);
        Assert.True(_hasher.Verify(hash, "Gizli-Parola-42"));
    }

    [Theory]
    [InlineData("gizli-parola-42")] // büyük/küçük harf farkı
    [InlineData("Gizli-Parola-4")]
    [InlineData("")]
    public void Yanlis_parola_dogrulanmaz(string attempt)
    {
        var hash = _hasher.Hash("Gizli-Parola-42");

        Assert.False(_hasher.Verify(hash, attempt));
    }

    [Fact]
    public void Ayni_parola_iki_kez_ozetlenince_farkli_ozet_cikar()
    {
        Assert.NotEqual(_hasher.Hash("Demo1234!"), _hasher.Hash("Demo1234!"));
    }

    [Fact]
    public void Bozuk_ya_da_bos_ozet_hata_vermeden_reddedilir()
    {
        Assert.False(_hasher.Verify("", "Demo1234!"));
        Assert.False(_hasher.Verify("AQAAAAIAAYagAAAAE-ornek-ozet", "Demo1234!"));
    }

    [Fact]
    public void Bos_parola_ozetlenmez()
    {
        Assert.ThrowsAny<ArgumentException>(() => _hasher.Hash(""));
    }
}
