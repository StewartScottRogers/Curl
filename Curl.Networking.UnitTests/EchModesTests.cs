namespace Curl.Networking;

/// <summary>Pins how <see cref="EchModes.Of" /> combines <c>--ech</c>'s mode and <c>ecl:</c> list as libcurl does (ADR-0327).</summary>
[TestClass]
public sealed class EchModesTests
{
    [TestMethod]
    [DataRow("false", null, EchMode.Off)]
    [DataRow("grease", "AAA=", EchMode.Grease)]
    [DataRow("true", null, EchMode.Opportunistic)]
    [DataRow("hard", "AAA=", EchMode.Mandatory)]
    [DataRow(null, "AAA=", EchMode.Opportunistic)]
    [DataRow("bogus", "AAA=", EchMode.Opportunistic)]
    [DataRow(null, null, EchMode.Off)]
    public void Of_CombinesTheModeAndTheList(string? mode, string? configList, EchMode expected) =>
        Assert.AreEqual(expected, EchModes.Of(new TlsClientOptions(Ech: mode, EchConfigList: configList)));

    [TestMethod]
    public void Of_WithNullOptions_ThrowsArgumentNullException() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => EchModes.Of(null!));
}
