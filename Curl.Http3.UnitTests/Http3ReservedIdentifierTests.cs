namespace Curl.Http3;

/// <summary>
/// Pins <see cref="Http3ReservedIdentifier" />: the grease form <c>0x1f * N + 0x21</c>.
/// </summary>
[TestClass]
public sealed class Http3ReservedIdentifierTests
{
    [TestMethod]
    [DataRow(0x20L, false)]
    [DataRow(0x21L, true)]
    [DataRow(0x22L, false)]
    [DataRow(0x40L, true)]
    [DataRow(0x5fL, true)]
    public void IsReserved_PinsTheGreaseForm(long value, bool expected) =>
        Assert.AreEqual(expected, Http3ReservedIdentifier.IsReserved(value));
}
