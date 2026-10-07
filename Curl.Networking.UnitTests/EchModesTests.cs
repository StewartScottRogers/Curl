using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins how <see cref="EchModes.Of" /> combines <c>--ech</c>'s mode with <c>pn:</c> and the
/// <c>ecl:</c> list as libcurl does (ADR-0327, ADR-0359; measured with curl 8.21.0 and OpenSSL 4.0.0).
/// </summary>
[TestClass]
public sealed class EchModesTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("false", null, null, EchMode.Off)]
    [DataRow("grease", null, "AAA=", EchMode.Grease)]
    [DataRow("true", null, null, EchMode.Opportunistic)]
    [DataRow("true", "pn.test", null, EchMode.Opportunistic)]
    [DataRow("hard", null, "AAA=", EchMode.Mandatory)]
    [DataRow(null, null, "AAA=", EchMode.Mandatory)]
    [DataRow(null, "pn.test", null, EchMode.Mandatory)]
    [DataRow("false", null, "AAA=", EchMode.Mandatory)]
    [DataRow("false", "pn.test", null, EchMode.Mandatory)]
    [DataRow(null, null, null, EchMode.Off)]
    public void Of_CombinesTheModeThePublicNameAndTheList(string? mode, string? publicName, string? configList, EchMode expected)
    {
        Diagnostics.Arrange("--ech mode", mode ?? "(none)");
        Diagnostics.Arrange("public name", publicName ?? "(none)");
        Diagnostics.Arrange("config list", configList ?? "(none)");

        var echMode = EchModes.Of(new TlsClientOptions(Ech: mode, EchPublicName: publicName, EchConfigList: configList));

        Diagnostics.Act("ECH mode", echMode);
        Diagnostics.Assert("ECH mode", expected, echMode);
        Assert.AreEqual(expected, echMode);
    }

    [TestMethod]
    public void Of_WithNullOptions_ThrowsArgumentNullException()
    {
        Diagnostics.Arrange("options", "null");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => EchModes.Of(null!));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("exception", nameof(ArgumentNullException), exception.GetType().Name);
    }
}
