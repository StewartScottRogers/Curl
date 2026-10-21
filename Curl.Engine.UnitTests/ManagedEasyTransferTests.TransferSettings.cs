using Curl.Api.Options;

namespace Curl.Engine;

/// <summary>
/// Pins the curl tool's silence settings an easy-interface transfer with no <c>ToolSettings</c> gets: silent and
/// showing no error, so a libcurl-API SFTP or SCP transfer with no known_hosts file writes no
/// <see cref="TransferPerformer.KnownHostsFileMissingLine" /> to standard error, as libcurl never does (AF-0296, BL-2934).
/// </summary>
public sealed partial class ManagedEasyTransferTests
{
    [TestMethod]
    public void TransferSettingsOf_DefaultOptions_IsSilentAndShowsNoError()
    {
        EngineTransferSettings settings = ManagedEasyTransfer.TransferSettingsOf(new EasyOptions());

        Assert.IsTrue(settings.Silent);
        Assert.IsFalse(settings.ShowError);
    }
}
