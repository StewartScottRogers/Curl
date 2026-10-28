using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// CGR-196: a transfer URL of 8,000,000 bytes exits 27 and a longer one exits 43, each with that code's own
/// text and no <c>-v</c> info line, as curl 8.22.0 does.
/// </summary>
public sealed partial class TransferPerformerTests
{
    [TestMethod]
    [DataRow(CurlUrlRejection.UrlAtLengthLimit, CurlExitCode.OutOfMemory, "Out of memory")]
    [DataRow(CurlUrlRejection.UrlTooLong, CurlExitCode.BadFunctionArgument, "A libcurl function was given a bad argument")]
    public void ReportUrlRejected_UrlLengthLimit_ExitsWithThatCodesOwnTextAndNoInfoLine(CurlUrlRejection rejection, CurlExitCode exitCode, string message)
    {
        InfoRecordingEvents events = new();

        TransferResult result = TransferPerformer.ReportUrlRejected(rejection, events);

        Assert.AreEqual(exitCode, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
        Assert.IsEmpty(events.Info);
    }

    [TestMethod]
    public void TryParseTransferUrl_UrlAboveTheLimit_IsRejectedAsTooLongBeforeParsing()
    {
        bool parsed = TransferPerformer.TryParseTransferUrl("http://h/" + new string('a', 8_000_000), disallowUsernameInUrl: false, pathAsIs: false, out CurlUrl? url, out CurlUrlRejection rejection);

        Assert.IsFalse(parsed);
        Assert.IsNull(url);
        Assert.AreEqual(CurlUrlRejection.UrlTooLong, rejection);
    }
}
