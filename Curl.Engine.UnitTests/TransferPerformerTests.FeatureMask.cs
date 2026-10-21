using Curl.Cli;
using Curl.Console;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins the run-time feature mask's proxy refusal (ADR-0746, BL-2984): under a dispatch hiding
/// <see cref="EngineFeatureMask.Proxy" /> a transfer with a proxy set answers <c>CURLE_NOT_BUILT_IN</c>
/// before anything else, and without the mask, or without a proxy, nothing changes.
/// </summary>
public sealed partial class TransferPerformerTests
{
    [TestMethod]
    public void TrySelectProxyAndCredentials_ProxyHiddenAndProxySet_FailsNotBuiltInWithNoText()
    {
        CommandLineOptions options = Parse("-x", "wohoo", First);

        bool selected = SelectProxyUnder(EngineFeatureMask.Proxy, options, out ProxyEndpoint? proxy, out TransferResult? failure);

        Assert.IsFalse(selected);
        Assert.IsNull(proxy);
        Assert.AreSame(TransferPerformer.ProxyNotBuiltInFailure, failure);
        Assert.AreEqual(CurlExitCode.NotBuiltIn, failure!.ExitCode);
        Assert.IsNull(failure.ErrorMessage);
    }

    [TestMethod]
    public void TrySelectProxyAndCredentials_ProxyHiddenAndNoProxySet_ConnectsDirectly()
    {
        CommandLineOptions options = Parse(First);

        bool selected = SelectProxyUnder(EngineFeatureMask.Proxy, options, out ProxyEndpoint? proxy, out TransferResult? failure);

        Assert.IsTrue(selected);
        Assert.IsNull(proxy);
        Assert.IsNull(failure);
    }

    [TestMethod]
    public void TrySelectProxyAndCredentials_OtherFeaturesHiddenAndProxySet_ChoosesThatProxy()
    {
        CommandLineOptions options = Parse("-x", "wohoo", First);

        bool selected = SelectProxyUnder(EngineFeatureMask.Ipv6 | EngineFeatureMask.Idn, options, out ProxyEndpoint? proxy, out TransferResult? failure);

        Assert.IsTrue(selected);
        Assert.IsNull(failure);
        Assert.AreEqual("wohoo", proxy!.Host);
    }

    [TestMethod]
    public void FeatureMask_NotSet_IsNone()
    {
        TransferDispatch dispatch = HttpDispatch();

        EngineFeatureMask mask = dispatch.FeatureMask;

        Assert.AreEqual(EngineFeatureMask.None, mask);
    }

    private static bool SelectProxyUnder(EngineFeatureMask mask, CommandLineOptions options, out ProxyEndpoint? proxy, out TransferResult? failure)
    {
        TransferDispatch dispatch = new(new ProtocolDispatcher([new ScriptedHandler()])) { FeatureMask = mask };
        return TransferPerformer.TrySelectProxyAndCredentials(
            dispatch, EasyOptionsFromCommandLine.TransferSettingsOf(options), EasyOptionsFromCommandLine.ProxyOf(options), EasyOptionsFromCommandLine.AuthenticationOf(options), CurlUrl.Parse(First), NewState(), new InfoRecordingEvents(), NoDiagnosticLog.Instance, CredentialLookup(),
            out proxy, out failure);
    }
}
