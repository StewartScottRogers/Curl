using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// CGR-059: a transfer to a host IDNA cannot convert fails as curl 8.22.0's does, exit 3 with
/// <c>URL using bad/illegal format or missing URL</c> and no <c>-v</c> info line.
/// </summary>
public sealed partial class TransferPerformerTests
{
    [TestMethod]
    public void ReportUrlRejected_UnconvertibleIdnHost_Exits3WithTheBareMalformatTextAndNoInfoLine()
    {
        InfoRecordingEvents events = new();

        bool parsed = TransferPerformer.TryParseTransferUrl("http://-ü.example/", disallowUsernameInUrl: false, pathAsIs: false, out _, out CurlUrlRejection rejection);
        TransferResult result = TransferPerformer.ReportUrlRejected(rejection, events);

        Assert.IsFalse(parsed);
        Assert.AreEqual(CurlUrlRejection.IdnConversionFailed, rejection);
        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.AreEqual("URL using bad/illegal format or missing URL", result.ErrorMessage);
        Assert.IsEmpty(events.Info);
    }

    [TestMethod]
    public void UrlRejectionText_BadHostname_KeepsTheUrlRejectedPrefix() =>
        Assert.AreEqual("URL rejected: Bad hostname", TransferPerformer.UrlRejectionText(CurlUrlRejection.BadHostname));
}
