using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.Composition;

[TestClass]
public sealed class DpopWithResumableUploadOrderTests
{
    [TestMethod]
    public void Catalogue_DpopWithResumableUpload_DpopWrapsTheUploadSoItsHeadersReachEveryUploadRequest()
    {
        var values = new ExtensionOptionValues(new Dictionary<string, string> { ["dpop"] = "k.pem", ["resumable-upload"] = "x" });

        var hooks = TransferHooks.ActivatedBy(values, ExtensionTransferHookCatalogue.Entries).Hooks;

        var names = hooks.Select(hook => hook.GetType().Name).ToList();
        Assert.IsTrue(names.IndexOf("DpopTransferHook") >= 0, string.Join(",", names));
        Assert.IsTrue(names.IndexOf("DpopTransferHook") < names.IndexOf("ResumableUploadTransferHook"), string.Join(",", names));
    }
}
