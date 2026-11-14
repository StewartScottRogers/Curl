using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Connection;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Composition;

/// <summary>
/// Pins ADR-1269: <c>--npipe</c> or <c>--vm-socket</c> beside curl's <c>--unix-socket</c> or
/// <c>--abstract-unix-socket</c> is refused naming both options, before any connection is opened, while each
/// alone, and <c>--npipe</c> with <c>--vm-socket</c>, keep their behaviour.
/// </summary>
[TestClass]
public sealed class ConnectionOptionConflictTests
{
    private const string PipeName = @"\\.\pipe\conflict";
    private const string VmAddress = "3:1234";

    [TestMethod]
    [DataRow("npipe", PipeName, "unix-socket")]
    [DataRow("npipe", PipeName, "abstract-unix-socket")]
    [DataRow("vm-socket", VmAddress, "unix-socket")]
    [DataRow("vm-socket", VmAddress, "abstract-unix-socket")]
    public void RefuseBesideCurlSocket_ExtensionOptionWithCurlSocket_RefusesNamingBothOptions(string option, string value, string curlSocketOption)
    {
        ConnectionHooks hooks = HooksFor((option, value));
        Assert.IsNotNull(hooks.Hook);

        ExtensionHookConflictException failure = Assert.ThrowsExactly<ExtensionHookConflictException>(
            () => CurlSocketConnectionConflict.RefuseBesideCurlSocket(hooks, curlSocketOption));

        Assert.AreEqual(
            $"the options --{option} and --{curlSocketOption} each supply the connection a transfer runs over; give only one of them",
            failure.Message);
    }

    [TestMethod]
    [DataRow("npipe", PipeName)]
    [DataRow("vm-socket", VmAddress)]
    public void RefuseBesideCurlSocket_ExtensionOptionAlone_IsNotRefused(string option, string value)
    {
        ConnectionHooks hooks = HooksFor((option, value));

        CurlSocketConnectionConflict.RefuseBesideCurlSocket(hooks, null);

        Assert.AreEqual(option, hooks.Hook!.Option.Name);
    }

    [TestMethod]
    [DataRow("unix-socket")]
    [DataRow("abstract-unix-socket")]
    public void RefuseBesideCurlSocket_CurlSocketAlone_IsNotRefused(string curlSocketOption)
    {
        ConnectionHooks hooks = HooksFor();

        CurlSocketConnectionConflict.RefuseBesideCurlSocket(hooks, curlSocketOption);

        Assert.IsNull(hooks.Hook);
    }

    [TestMethod]
    public void ActivatedBy_NamedPipeWithVmSocket_KeepsItsRefusal()
    {
        ExtensionHookConflictException failure = Assert.ThrowsExactly<ExtensionHookConflictException>(
            () => HooksFor(("npipe", PipeName), ("vm-socket", VmAddress)));

        Assert.AreEqual(
            "the options --npipe and --vm-socket each supply the connection a transfer runs over; give only one of them",
            failure.Message);
    }

    [TestMethod]
    public void RefuseBesideCurlSocket_HooksNull_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlSocketConnectionConflict.RefuseBesideCurlSocket(null!, "unix-socket"));
    }

    [TestMethod]
    public void RefuseBesideCurlSocket_Refused_OpensNoConnection()
    {
        OpeningRecordingHook hook = new();
        ConnectionHooks hooks = ConnectionHooks.ActivatedBy(ExtensionOptionValues.None, [_ => hook]);

        Assert.ThrowsExactly<ExtensionHookConflictException>(() => CurlSocketConnectionConflict.RefuseBesideCurlSocket(hooks, "unix-socket"));

        Assert.IsFalse(hook.AskedForConnector);
    }

    private static ConnectionHooks HooksFor(params (string Name, string Value)[] given) =>
        ConnectionHooks.ActivatedBy(
            new ExtensionOptionValues(given.ToDictionary(option => option.Name, option => option.Value)),
            ExtensionConnectionHookCatalogue.Entries);

    private sealed class OpeningRecordingHook : IConnectionHook
    {
        public ExtensionOption Option { get; } = new("test-pipe", ExtensionOptionArgument.Value, "Test pipe");

        public bool AskedForConnector { get; private set; }

        public IConnector ConnectorOver(IConnector curlConnector)
        {
            AskedForConnector = true;
            return curlConnector;
        }
    }
}
