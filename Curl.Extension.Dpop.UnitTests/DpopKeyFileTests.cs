using System.Security.Cryptography;

namespace Curl.Extension.Dpop.UnitTests;

[TestClass]
public sealed class DpopKeyFileTests
{
    private string _directory = null!;

    [TestInitialize]
    public void CreateDirectory() => _directory = Directory.CreateTempSubdirectory("dpop-").FullName;

    [TestCleanup]
    public void DeleteDirectory() => Directory.Delete(_directory, recursive: true);

    [TestMethod]
    public async Task ReadAsync_AFileWithAPrivateKey_GivesTheKey()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var key = await DpopKeyFile.ReadAsync(Write(ecdsa.ExportECPrivateKeyPem()), CancellationToken.None);

        Assert.AreEqual("ES256", key!.Algorithm);
    }

    [TestMethod]
    public async Task ReadAsync_AFileThatCannotBeReadOrHoldsNoPrivateKey_GivesNone()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        Assert.IsNull(await DpopKeyFile.ReadAsync(Path.Combine(_directory, "missing.pem"), CancellationToken.None));
        Assert.IsNull(await DpopKeyFile.ReadAsync(_directory, CancellationToken.None));
        Assert.IsNull(await DpopKeyFile.ReadAsync(Write("not a key"), CancellationToken.None));
        Assert.IsNull(await DpopKeyFile.ReadAsync(Write(ecdsa.ExportSubjectPublicKeyInfoPem()), CancellationToken.None));
    }

    private string Write(string text)
    {
        var path = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".pem");
        File.WriteAllText(path, text);
        return path;
    }
}
