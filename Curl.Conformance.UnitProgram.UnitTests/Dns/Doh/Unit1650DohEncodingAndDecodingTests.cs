using System.Net;
using System.Text;
using Curl.Conformance.UnitProgram.Dns.Doh;
using Curl.Networking;

namespace Curl.Conformance.UnitProgram.UnitTests.Dns.Doh;

[TestClass]
public sealed class Unit1650DohEncodingAndDecodingTests
{
    [TestMethod]
    public async Task RunAsync_OverTheEncoderAndDecoder_EveryCheckMatchesAndEachIsAnnounced()
    {
        IUnitProgram program = UnitPrograms.Ported.Find("unit1650")!;
        using MemoryStream standardOutput = new();
        using StringWriter standardError = new();

        UnitProgramResult result = await program.RunAsync(new UnitProgramContext(1650, []) { StandardOutput = standardOutput, StandardError = standardError }, CancellationToken.None);

        string output = Encoding.UTF8.GetString(standardOutput.ToArray());
        Assert.AreEqual(string.Empty, standardError.ToString());
        Assert.AreEqual(0, result.ExitCode);
        Assert.StartsWith("test 0: req 0: test.host.name A\n", output);
        Assert.Contains("test 14: resp 11: self-pointing CNAME\n", output);
        Assert.EndsWith("test 115: full49 whole\n", output);
    }

    [TestMethod]
    public async Task RunAsync_CodecThatAlwaysSucceedsEmpty_CountsEveryCheckThatExpectsAnotherResult()
    {
        Unit1650DohEncodingAndDecoding program = new((_, _) => new DnsQueryEncoding([], DnsMessageFailure.None), (_, _) => new DnsAnswer(DnsMessageFailure.None, [], 0));
        using StringWriter standardError = new();

        UnitProgramResult result = await program.RunAsync(new UnitProgramContext(1650, []) { StandardError = standardError }, CancellationToken.None);

        // All 3 request rows, 12 of the 14 response rows (the two CNAME rows expect None alone),
        // the 49 prefixes, the 49 suffixes and the whole answer.
        Assert.AreEqual(3 + 12 + 49 + 49 + 1, result.ExitCode);
        Assert.StartsWith("Test req 0: test.host.name A (0) failed\nExpected: None|000001000001000000000000", standardError.ToString());
    }

    [TestMethod]
    public async Task RunAsync_NullContext_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new Unit1650DohEncodingAndDecoding().RunAsync(null!, CancellationToken.None));
    }

    [TestMethod]
    public void RenderRequest_NullEncoding_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Unit1650DohEncodingAndDecoding.RenderRequest(null!));
    }

    [TestMethod]
    public void RenderResponse_NullAnswer_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Unit1650DohEncodingAndDecoding.RenderResponse(null!, withAddresses: true));
    }

    [TestMethod]
    public void RenderResponse_AnIpv4AndAnIpv6Address_PrintsEachAsTheCProgramDoes()
    {
        DnsAnswer answer = new(DnsMessageFailure.None, [IPAddress.Parse("10.0.0.1"), IPAddress.Parse("2001:db8::1")], 7);

        string rendered = Unit1650DohEncodingAndDecoding.RenderResponse(answer, withAddresses: true);

        Assert.AreEqual("None|10.0.0.1 2001:0db8:0000:0000:0000:0000:0000:0001 (7)", rendered);
    }

    [TestMethod]
    public void RenderResponse_WithoutAddresses_IsTheFailureAlone()
    {
        Assert.AreEqual("OutOfRange", Unit1650DohEncodingAndDecoding.RenderResponse(new DnsAnswer(DnsMessageFailure.OutOfRange, [], 0), withAddresses: false));
    }
}
