using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Plugin;

/// <summary>Checks a helper whose output ends inside its capabilities list, against ADR-1142.</summary>
[TestClass]
public sealed class HelperCapabilitiesEndTests
{
    [TestMethod]
    [DataRow("", false)]
    [DataRow("get", false)]
    [DataRow("", true)]
    [DataRow("put", true)]
    public async Task Execute_OutputEndsBeforeTheEmptyLine_Exit8(string output, bool isUpload)
    {
        TransferResult result = await Run(new ScriptedHelperProcess(output), isUpload);

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("curl-proto-demo closed its output before ending its capabilities", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Execute_HelperExitsNonZeroBeforeTheEmptyLine_Exit56(bool isUpload)
    {
        TransferResult result = await Run(new ScriptedHelperProcess("", exitStatus: 3), isUpload);

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("curl-proto-demo exited with status 3", result.ErrorMessage);
    }

    [TestMethod]
    public async Task Execute_CapabilitiesEndedButOnlyPut_StillLacksGet()
    {
        TransferResult result = await Run(new ScriptedHelperProcess("put\n\n"), false);

        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual("curl-proto-demo does not offer the get capability", result.ErrorMessage);
    }

    [TestMethod]
    public async Task Execute_GetAndEmptyLine_Transfers()
    {
        TransferResult result = await Run(new ScriptedHelperProcess("get\n\nok\n"), false);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
    }

    [TestMethod]
    public async Task Execute_MandatoryUnknownCapability_IsRefused()
    {
        TransferResult result = await Run(new ScriptedHelperProcess("*x\n\nok\n"), false);

        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual("curl-proto-demo requires the unknown capability x", result.ErrorMessage);
    }

    private static async Task<TransferResult> Run(ScriptedHelperProcess helper, bool isUpload)
    {
        HelperProtocolHandler handler = new(new Dictionary<string, string> { ["demo"] = "/bin/curl-proto-demo" }, new ScriptedHelperProcessStarter(helper));
        TransferContext context = new() { Url = CurlUrl.Parse("demo://host/a"), Output = new MemoryStream(), PostData = isUpload ? new ReadOnlyMemory<byte>("z"u8.ToArray()) : (ReadOnlyMemory<byte>?)null };
        return await handler.ExecuteAsync(context);
    }
}
