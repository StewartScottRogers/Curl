using System.Security.Cryptography;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// CGR-092 of the third-party gap report (BL-3621): curl 8.22.0 reads a <c>--pinnedpubkey</c>
/// <c>sha256//</c> list hash after hash, each running to the next <c>;</c>, and goes on only while
/// that <c>;</c> is followed by another <c>sha256//</c>. So a trailing <c>;</c> or text after it
/// leaves a correct pin matching, and <c>;;</c> or any other text ends the list before a later pin
/// (measured against curl 8.22.0 Schannel, 2026-10-26, ADR-0193).
/// </summary>
[TestClass]
public sealed class Cgr092Tests
{
    private static readonly byte[] s_subjectPublicKeyInfo = CreateSubjectPublicKeyInfo();

    private static readonly string s_hash = PinnedPublicKey.HashOf(s_subjectPublicKeyInfo);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("sha256//{0};", true)]
    [DataRow("sha256//{0};x", true)]
    [DataRow("sha256//{0};;", true)]
    [DataRow("sha256//AAAA=;sha256//{0}", true)]
    [DataRow("sha256//AAAA=;sha256//{0};", true)]
    [DataRow("sha256//AAAA=;sha256//{0};x", true)]
    [DataRow("sha256//;sha256//{0}", true)]
    [DataRow("sha256//AAAA=;;sha256//{0}", false)]
    [DataRow("sha256//AAAA=;x;sha256//{0}", false)]
    [DataRow("sha256//AAAA=; sha256//{0}", false)]
    [DataRow("sha256//AAAA=;SHA256//{0}", false)]
    [DataRow("sha256//AAAA=;xsha256//{0}", false)]
    [DataRow("sha256//AAAA=sha256//{0}", false)]
    public void Matches_ASha256List_SplitsItAsCurlDoes(string pinFormat, bool expected)
    {
        var pin = string.Format(System.Globalization.CultureInfo.InvariantCulture, pinFormat, s_hash);
        Diagnostics.Arrange("pin", pinFormat);

        var matches = PinnedPublicKey.Matches(pin, s_subjectPublicKeyInfo);

        Diagnostics.Act("matches", matches);
        Assert.AreEqual(expected, matches);
    }

    private static byte[] CreateSubjectPublicKeyInfo()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return key.ExportSubjectPublicKeyInfo();
    }
}
