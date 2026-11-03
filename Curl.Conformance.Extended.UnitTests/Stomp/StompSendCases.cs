using System.Text;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.Stomp;

/// <summary>
/// Checks curl's SEND frame and the RECEIPT it waits for against STOMP 1.2 (BL-4493): <c>content-length</c> over a body
/// holding NUL, header escapes, UTF-8, <c>content-type</c>, and a missing or mismatched receipt.
/// </summary>
[TestClass]
public sealed class StompSendCases
{
    [TestMethod]
    [SpecRequirement("Stomp:STOMP12-ContentLength-2")]
    public async Task Publish_BodyHoldingNul_SendsItsWholeOctetCountAsContentLength()
    {
        string directory = Directory.CreateTempSubdirectory("stomp-").FullName;
        try
        {
            string body = Path.Combine(directory, "body.bin");
            await File.WriteAllBytesAsync(body, [(byte)'a', 0, (byte)'b']);

            CurlRunResult result = await StompRun.RunAsync(StompRun.PublishAnswered(), "--data-binary", "@" + body, "stomp://localhost/q");

            StompRun.AssertExit(result, 0, string.Empty);
            StringAssert.Contains(StompRun.ClientText(result), "SEND\ndestination:/q\ncontent-length:3\ncontent-type:application/octet-stream\n\na\0b\0" + StompRun.Disconnect);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    [SpecRequirement("Stomp:STOMP12-Encoding-2")]
    public async Task Publish_DestinationWithColonAndBackslash_IsEscaped()
    {
        CurlRunResult result = await StompRun.RunAsync(StompRun.PublishAnswered(), "-d", "hi", "stomp://localhost/a%3Ab%5Cc");

        StompRun.AssertExit(result, 0, string.Empty);
        StringAssert.Contains(StompRun.ClientText(result), "SEND\ndestination:/a\\cb\\\\c\ncontent-length:2\ncontent-type:application/octet-stream\n\n");
    }

    [TestMethod]
    [SpecRequirement("Stomp:STOMP12-Encoding-1")]
    public async Task Publish_NonAsciiDestination_IsSentAsUtf8()
    {
        CurlRunResult result = await StompRun.RunAsync(StompRun.PublishAnswered(), "-d", "hi", "stomp://localhost/caf%C3%A9");

        StompRun.AssertExit(result, 0, string.Empty);
        StringAssert.Contains(StompRun.ClientText(result), "destination:/" + Encoding.Latin1.GetString(Encoding.UTF8.GetBytes("café")) + "\n");
    }

    [TestMethod]
    [SpecRequirement("Stomp:STOMP12-ContentType-1")]
    public Task Publish_Body_SendsAContentType() =>
        StompRun.Known.RunCaseAsync(
            ["Stomp:STOMP12-ContentType-1"],
            async () =>
            {
                CurlRunResult result = await StompRun.RunAsync(StompRun.PublishAnswered(), "-d", "hi", "stomp://localhost/q");

                StompRun.AssertExit(result, 0, string.Empty);
                StringAssert.Matches(StompRun.ClientText(result), new System.Text.RegularExpressions.Regex("SEND\n[^\0]*\ncontent-type:[^\n]+\n"));
            });

    [TestMethod]
    [SpecRequirement("Stomp:STOMP12-Receipt-2")]
    public async Task Publish_ServerClosesWithoutReceipt_FailsAsDisconnected()
    {
        CurlRunResult result = await StompRun.RunAsync(StompRun.PublishAnswered(receipt: null), "-d", "hi", "stomp://localhost/q");

        StompRun.AssertExit(result, 56, "curl: (56) Connection disconnected\n");
        StringAssert.EndsWith(StompRun.ClientText(result), StompRun.Disconnect);
    }

    [TestMethod]
    [SpecRequirement("Stomp:STOMP12-Receipt-1")]
    public async Task Publish_ReceiptForAnotherId_IsAnUnexpectedFrame()
    {
        CurlRunResult result = await StompRun.RunAsync(StompRun.PublishAnswered("RECEIPT\nreceipt-id:77\n\n\0"), "-d", "hi", "stomp://localhost/q");

        StompRun.AssertExit(result, 8, "curl: (8) Unexpected STOMP RECEIPT frame\n");
    }
}
