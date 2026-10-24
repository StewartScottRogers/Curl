using System.Runtime.CompilerServices;

namespace Curl.Conformance;

internal static partial class UpstreamPerlCommandScripts
{
    [ModuleInitializer]
    internal static void RegisterTest1167Script() => Register(UpstreamTest1167Script.ScriptName, UpstreamTest1167Script.RunAsync);
}
