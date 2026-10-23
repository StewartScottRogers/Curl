using System.Runtime.CompilerServices;

namespace Curl.Conformance;

internal static partial class UpstreamPerlCommandScripts
{
    [ModuleInitializer]
    internal static void RegisterTest745Script() => Register(UpstreamTest745Script.ScriptName, UpstreamTest745Script.RunAsync);
}
