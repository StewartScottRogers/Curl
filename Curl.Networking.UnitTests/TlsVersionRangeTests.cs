using System.Security.Authentication;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins the <see cref="SslProtocols" /> set <see cref="TlsVersionRange" /> offers for every
/// minimum and ceiling pair curl's <c>-1</c>/<c>--tlsv1.x</c> and <c>--tls-max</c> can make
/// (BL-502). TLS 1.0 and TLS 1.1 are named through <see cref="TlsVersionRange.Tls10" /> and
/// <see cref="TlsVersionRange.Tls11" />, so the obsolete members stay in one place.
/// </summary>
[TestClass]
public sealed class TlsVersionRangeTests
{
    private const SslProtocols Tls10 = TlsVersionRange.Tls10;

    private const SslProtocols Tls11 = TlsVersionRange.Tls11;

    private const SslProtocols Tls12 = SslProtocols.Tls12;

    private const SslProtocols Tls13 = SslProtocols.Tls13;

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(TlsVersion.SystemDefault, TlsVersion.SystemDefault, SslProtocols.None)]
    [DataRow(TlsVersion.SystemDefault, TlsVersion.Tls13, SslProtocols.None)]
    [DataRow(TlsVersion.SystemDefault, TlsVersion.Tls12, Tls10 | Tls11 | Tls12)]
    [DataRow(TlsVersion.SystemDefault, TlsVersion.Tls11, Tls10 | Tls11)]
    [DataRow(TlsVersion.SystemDefault, TlsVersion.Tls10, Tls10)]
    [DataRow(TlsVersion.Tls10, TlsVersion.SystemDefault, Tls10 | Tls11 | Tls12 | Tls13)]
    [DataRow(TlsVersion.Tls10, TlsVersion.Tls13, Tls10 | Tls11 | Tls12 | Tls13)]
    [DataRow(TlsVersion.Tls10, TlsVersion.Tls12, Tls10 | Tls11 | Tls12)]
    [DataRow(TlsVersion.Tls10, TlsVersion.Tls11, Tls10 | Tls11)]
    [DataRow(TlsVersion.Tls10, TlsVersion.Tls10, Tls10)]
    [DataRow(TlsVersion.Tls11, TlsVersion.SystemDefault, Tls11 | Tls12 | Tls13)]
    [DataRow(TlsVersion.Tls11, TlsVersion.Tls12, Tls11 | Tls12)]
    [DataRow(TlsVersion.Tls11, TlsVersion.Tls11, Tls11)]
    [DataRow(TlsVersion.Tls12, TlsVersion.SystemDefault, Tls12 | Tls13)]
    [DataRow(TlsVersion.Tls12, TlsVersion.Tls13, Tls12 | Tls13)]
    [DataRow(TlsVersion.Tls12, TlsVersion.Tls12, Tls12)]
    [DataRow(TlsVersion.Tls13, TlsVersion.SystemDefault, Tls13)]
    [DataRow(TlsVersion.Tls13, TlsVersion.Tls13, Tls13)]
    public void ToSslProtocols_MinimumAndCeiling_OffersEveryVersionBetweenThem(TlsVersion minimum, TlsVersion maximum, SslProtocols expected)
    {
        Diagnostics.Arrange("minimum, maximum", $"{minimum}, {maximum}");

        var offered = TlsVersionRange.ToSslProtocols(minimum, maximum);

        Diagnostics.Act("offered", (int)offered);
        Diagnostics.Assert("offered", (int)expected, (int)offered);

        Assert.AreEqual(expected, offered);
    }

    [TestMethod]
    [DataRow(TlsVersion.Tls13, TlsVersion.Tls12)]
    [DataRow(TlsVersion.Tls12, TlsVersion.Tls11)]
    [DataRow(TlsVersion.Tls11, TlsVersion.Tls10)]
    public void ToSslProtocols_MinimumAboveTheCeiling_ThrowsArgumentException(TlsVersion minimum, TlsVersion maximum)
    {
        Diagnostics.Arrange("minimum, maximum", $"{minimum}, {maximum}");

        var exception = Assert.ThrowsExactly<ArgumentException>(() => TlsVersionRange.ToSslProtocols(minimum, maximum));

        Diagnostics.Act("parameter name", exception.ParamName);
        Diagnostics.Assert("parameter name", "minimum", exception.ParamName);

        Assert.AreEqual("minimum", exception.ParamName);
    }
}
