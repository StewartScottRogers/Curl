using System.Runtime.CompilerServices;

namespace Curl.Conformance;

internal static partial class UpstreamPerlCommandScripts
{
    [ModuleInitializer]
    internal static void RegisterManagenScript() =>
        Register(UpstreamManagenScript.ScriptName, new UpstreamManagenScript(TimeProvider.System).RunAsync);
}
