using DisKlinigi.Core.Domain;

namespace DisKlinigi.Tests.Domain;

public class BranchTests
{
    [Fact]
    public void Yeni_sube_gecerli_bilgilerle_olusturulur()
    {
        var branch = new Branch("  Serdivan Şubesi ", "Sakarya", "Üniversite Cad.", chairCount: 4);

        Assert.Equal("Serdivan Şubesi", branch.Name);
        Assert.Equal(4, branch.ChairCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void Unit_sayisi_birden_az_olamaz(int chairCount)
    {
        var ex = Assert.Throws<DomainException>(
            () => new Branch("Serdivan Şubesi", "Sakarya", "Üniversite Cad.", chairCount));

        Assert.Contains("en az bir ünit", ex.Message);
    }

    [Fact]
    public void Sube_adi_bos_olamaz()
    {
        Assert.Throws<DomainException>(() => new Branch(" ", "Sakarya", "", chairCount: 2));
    }
}
