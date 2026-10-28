using Curl.Api.Codes;
using Curl.Api.EngineContracts;

namespace Curl.Engine;

/// <summary>CGR-246: <c>curl_easy_pause</c> on a handle with no transfer in progress is <c>CURLE_BAD_FUNCTION_ARGUMENT</c>.</summary>
[TestClass]
public sealed class Cgr246Tests
{
    [TestMethod]
    [DataRow(CurlPause.Continue)]
    [DataRow(CurlPause.Receive)]
    [DataRow(CurlPause.Send)]
    [DataRow(CurlPause.All)]
    public async Task Pause_NoPerformRunning_IsBadFunctionArgumentAndRecordsNothing(CurlPause directions)
    {
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)new ManagedCurlEngine().CreateEasy();

        CurlCode code = easy.Pause(directions);

        Assert.AreEqual(CurlCode.BadFunctionArgument, code);
        Assert.AreEqual(CurlPause.Continue, easy.PausedDirections);
    }
}
