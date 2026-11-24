using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.Composition;

[TestClass]
public sealed class DpopWithOAuth2OrderTests
{
    [TestMethod]
    public void Catalogue_DpopWithAnOAuth2Flow_DpopRunsFirstSoTheOAuth2HookSeesItsProofAndRefuses()
    {
        var values = new ExtensionOptionValues(new Dictionary<string, string>
        {
            ["dpop"] = "k.pem",
            ["oauth2-client-credentials"] = "https://server.example.com/token",
            ["oauth2-client-id"] = "cid",
        });

        var hooks = TransferHooks.ActivatedBy(values, ExtensionTransferHookCatalogue.Entries).Hooks;

        var names = hooks.Select(hook => hook.GetType().Name).ToList();
        Assert.IsTrue(names.IndexOf("DpopTransferHook") >= 0, string.Join(",", names));
        Assert.IsTrue(names.IndexOf("DpopTransferHook") < names.IndexOf("OAuth2TransferHook"), string.Join(",", names));
    }
}
