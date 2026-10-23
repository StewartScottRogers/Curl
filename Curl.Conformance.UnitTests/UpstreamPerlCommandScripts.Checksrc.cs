using System.Runtime.CompilerServices;

namespace Curl.Conformance;

internal static partial class UpstreamPerlCommandScripts
{
    [ModuleInitializer]
    internal static void RegisterChecksrcScript() => Register(UpstreamChecksrcScript.ScriptName, UpstreamChecksrcScript.RunAsync);
}
