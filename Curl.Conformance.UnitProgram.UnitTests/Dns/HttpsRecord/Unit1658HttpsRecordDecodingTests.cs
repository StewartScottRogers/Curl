using System.Net;
using System.Text;
using Curl.Conformance.UnitProgram.Dns.HttpsRecord;
using Curl.Networking;

namespace Curl.Conformance.UnitProgram.UnitTests.Dns.HttpsRecord;

[TestClass]
public sealed class Unit1658HttpsRecordDecodingTests
{
    [TestMethod]
    public async Task RunAsync_OverTheDecoder_EveryRowMatchesAndEachIsAnnounced()
    {
        IUnitProgram program = UnitPrograms.Ported.Find("unit1658")!;
        using MemoryStream standardOutput = new();
        using StringWriter standardError = new();

        UnitProgramResult result = await program.RunAsync(new UnitProgramContext(1658, []) { StandardOutput = standardOutput, StandardError = standardError }, CancellationToken.None);

        string output = Encoding.UTF8.GetString(standardOutput.ToArray());
        Assert.AreEqual(string.Empty, standardError.ToString());
        Assert.AreEqual(0, result.ExitCode);
        Assert.StartsWith("test 0: single h2 alpn\ntest 1: single h2 alpn missing last byte\n", output);
        Assert.EndsWith("test 29: rname long label\n", output);
    }

    [TestMethod]
    public async Task RunAsync_DecoderThatAlwaysFails_CountsEveryRowThatExpectsAnotherResult()
    {
        Unit1658HttpsRecordDecoding program = new(_ => new ServiceBindingDecoding(ServiceBindingFailure.ParameterOverrun, null));
        using StringWriter standardError = new();

        UnitProgramResult result = await program.RunAsync(new UnitProgramContext(1658, []) { StandardError = standardError }, CancellationToken.None);

        // Five of the 30 rows expect r:8.
        Assert.AreEqual(25, result.ExitCode);
        Assert.StartsWith("Test single h2 alpn (0) failed\nExpected: r:0|p:0|name.|alpn:10|\nReceived: r:8|\n", standardError.ToString());
    }

    [TestMethod]
    public async Task RunAsync_NullContext_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new Unit1658HttpsRecordDecoding().RunAsync(null!, CancellationToken.None));
    }

    [TestMethod]
    [DataRow(ServiceBindingFailure.Truncated, "r:43|")]
    [DataRow(ServiceBindingFailure.BadTargetName, "r:27|")]
    [DataRow(ServiceBindingFailure.TargetNameHasControlCharacter, "r:8|")]
    [DataRow(ServiceBindingFailure.ParametersOutOfOrder, "r:8|")]
    [DataRow(ServiceBindingFailure.ParameterOverrun, "r:8|")]
    [DataRow(ServiceBindingFailure.BadApplicationProtocolList, "r:61|")]
    [DataRow(ServiceBindingFailure.BadParameterValue, "r:43|")]
    public void Render_AFailure_IsTheCurlCodeCurlReturns(ServiceBindingFailure failure, string expected)
    {
        Assert.AreEqual(expected, Unit1658HttpsRecordDecoding.Render(new ServiceBindingDecoding(failure, null)));
    }

    [TestMethod]
    public void Render_TargetWithANul_EndsWhereCurlsCStringEnds()
    {
        ServiceBindingRecord record = new(1, "a\0b");

        Assert.AreEqual("r:0|p:1|a|", Unit1658HttpsRecordDecoding.Render(new ServiceBindingDecoding(ServiceBindingFailure.None, record)));
    }

    [TestMethod]
    public void Render_AlpnIdentifiers_KeepsCurlsKnownIdsOnceInOrder()
    {
        ServiceBindingRecord record = new(1, ".") { ApplicationProtocols = ["h3", "http/1.1", "h1", "x", "h2", "h3"], IPv4Hints = [IPAddress.Parse("10.0.0.1")] };

        Assert.AreEqual("r:0|p:1|.|alpn:20|alpn:8|alpn:10|ipv4:10.0.0.1|", Unit1658HttpsRecordDecoding.Render(new ServiceBindingDecoding(ServiceBindingFailure.None, record)));
    }

    [TestMethod]
    public void Render_NullDecoding_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Unit1658HttpsRecordDecoding.Render(null!));
    }
}
