using Curl.Cli;
using Curl.Engine;
using Curl.Testing;

namespace Curl.Console;

/// <summary>Pins that <c>-G</c> moves the <c>-d</c> data into the query, so the transfer settings carry no post data (BL-5125).</summary>
[TestClass]
public sealed class GetModeTransferSettingsTests
{
    private const string Url = "http://example.com/";

    [TestMethod]
    public void TransferSettingsOf_DataWithGet_CarriesNoPostData()
    {
        Assert.IsNull(EasyOptionsFromCommandLine.TransferSettingsOf(Parse("-G", "-d", "a=1")).PostData);
    }

    [TestMethod]
    public void TransferSettingsOf_DataWithoutGet_CarriesThePostData()
    {
        Assert.AreEqual("a=1", System.Text.Encoding.ASCII.GetString(EasyOptionsFromCommandLine.TransferSettingsOf(Parse("-d", "a=1")).PostData!.Value.Span));
    }

    private static CommandLineOptions Parse(params string[] arguments)
    {
        CommandLineParseResult result = QuicBuildParser.Parse([.. arguments, Url], _ => true);
        Assert.IsTrue(result.IsAccepted);
        return result.Options;
    }
}
