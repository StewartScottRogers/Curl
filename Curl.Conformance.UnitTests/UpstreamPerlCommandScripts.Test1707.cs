using System.Runtime.CompilerServices;

namespace Curl.Conformance;

internal static partial class UpstreamPerlCommandScripts
{
    // The script reads the vendored test data folder, looked up only when a case runs it.
    [ModuleInitializer]
    internal static void RegisterTest1707Script() =>
        Register(UpstreamTest1707Script.ScriptName, (invocation, runCurl) => new UpstreamTest1707Script(UpstreamConformanceTests.UpstreamTestDataFolder).RunAsync(invocation, runCurl));
}
