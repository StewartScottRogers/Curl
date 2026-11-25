using Curl.Api.Options.Authentication;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Api.Options.Proxy;
using Curl.Cli;
using Curl.Console;
using Curl.Core;
using Curl.Protocol.Abstractions;
using EasyProtocolOptions = Curl.Api.Options.Protocols.EasyProtocolOptions;

namespace Curl.Engine;

/// <summary>
/// Pins that <see cref="TransferContextFactory" /> puts the transfer's no-proxy list on
/// <see cref="TransferContext.NoProxy" />: <c>--noproxy</c>, else <c>no_proxy</c>, else <c>NO_PROXY</c>.
/// </summary>
[TestClass]
public sealed class TransferContextFactoryNoProxyTests
{
    [TestMethod]
    public void Create_NoProxyOptionGiven_ContextCarriesItOverTheEnvironment()
    {
        string? noProxy = ContextWith("a.example", ["no_proxy", "env.example"]).NoProxy;

        Assert.AreEqual("a.example", noProxy);
    }

    [TestMethod]
    public void Create_NoProxyOptionEmpty_ContextCarriesTheEmptyListNotTheEnvironment()
    {
        string? noProxy = ContextWith("", ["no_proxy", "env.example"]).NoProxy;

        Assert.AreEqual("", noProxy);
    }

    [TestMethod]
    public void Create_LowerCaseVariableAndUpperCaseVariable_LowerCaseWins()
    {
        string? noProxy = ContextWith(null, ["no_proxy", "lower.example", "NO_PROXY", "upper.example"]).NoProxy;

        Assert.AreEqual("lower.example", noProxy);
    }

    [TestMethod]
    public void Create_OnlyUpperCaseVariable_ContextCarriesIt()
    {
        string? noProxy = ContextWith(null, ["NO_PROXY", "upper.example"]).NoProxy;

        Assert.AreEqual("upper.example", noProxy);
    }

    [TestMethod]
    public void Create_NeitherOptionNorVariable_NoProxyIsNull()
    {
        Assert.IsNull(ContextWith(null, []).NoProxy);
    }

    private static TransferContext ContextWith(string? noProxyOption, string[] environment)
    {
        Dictionary<string, string> variables = [];
        for (int index = 0; index < environment.Length; index += 2)
        {
            variables[environment[index]] = environment[index + 1];
        }

        using MemoryStream standardInput = new();
        using MemoryStream output = new();
        string[] arguments = ["file:///x.txt"];
        CommandLineOptions options = CommandLineParser.Parse(arguments, _ => true).Options!;
        return new TransferContextFactory(standardInput) { ReadEnvironmentVariable = name => variables.GetValueOrDefault(name) }
            .Create(EasyOptionsFromCommandLine.TransferSettingsOf(options), EasyOptionsFromCommandLine.HttpOf(options), EasyOptionsFromCommandLine.BehaviourOf(options), EasyOptionsFromCommandLine.TransferOf(options), new EasyProtocolOptions(), new EasyProxyOptions { NoProxy = noProxyOption }, new EasyAuthenticationOptions(), new EasyNetworkOptions(), CurlUrl.Parse("file:///x.txt"), output, null, null, null);
    }
}
