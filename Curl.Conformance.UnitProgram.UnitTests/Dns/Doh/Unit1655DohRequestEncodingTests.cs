using System.Text;
using Curl.Conformance.UnitProgram.Dns.Doh;
using Curl.Networking;

namespace Curl.Conformance.UnitProgram.UnitTests.Dns.Doh;

[TestClass]
public sealed class Unit1655DohRequestEncodingTests
{
    [TestMethod]
    public async Task RunAsync_OverTheEncoder_EveryRowMatchesAndEachIsAnnounced()
    {
        IUnitProgram program = UnitPrograms.Ported.Find("unit1655")!;
        using MemoryStream standardOutput = new();
        using StringWriter standardError = new();

        UnitProgramResult result = await program.RunAsync(new UnitProgramContext(1655, []) { StandardOutput = standardOutput, StandardError = standardError }, CancellationToken.None);

        string output = Encoding.UTF8.GetString(standardOutput.ToArray());
        Assert.AreEqual(string.Empty, standardError.ToString());
        Assert.AreEqual(0, result.ExitCode);
        Assert.StartsWith("test 0: name too long\ntest 1: empty label\n", output);
        Assert.EndsWith("test 7: maximum-length name, buffer one byte too small\n", output);
    }

    [TestMethod]
    public async Task RunAsync_EncoderThatAlwaysRefuses_CountsEveryRowThatExpectsAnotherResult()
    {
        Unit1655DohRequestEncoding program = new((_, _) => new DnsQueryEncoding([], DnsMessageFailure.BadLabel));
        using StringWriter standardError = new();

        UnitProgramResult result = await program.RunAsync(new UnitProgramContext(1655, []) { StandardError = standardError }, CancellationToken.None);

        // Two of the eight rows expect BadLabel.
        Assert.AreEqual(6, result.ExitCode);
        Assert.StartsWith("Test name too long (0) failed\nExpected: NameTooLong|0\nReceived: BadLabel|0\n", standardError.ToString());
    }

    [TestMethod]
    public async Task RunAsync_NullContext_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new Unit1655DohRequestEncoding().RunAsync(null!, CancellationToken.None));
    }

    [TestMethod]
    public void Render_NullEncoding_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Unit1655DohRequestEncoding.Render(null!, 1));
    }

    [TestMethod]
    [DataRow(3, "None|3")]
    [DataRow(2, "TooSmall|0")]
    public void Render_AnEncodedQuery_FitsOrIsTooSmallForTheBuffer(int bufferLength, string expected)
    {
        Assert.AreEqual(expected, Unit1655DohRequestEncoding.Render(new DnsQueryEncoding([1, 2, 3], DnsMessageFailure.None), bufferLength));
    }
}
