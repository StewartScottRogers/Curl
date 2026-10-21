using System.Runtime.CompilerServices;

namespace Curl.Conformance;

internal static partial class UpstreamPerlCommandScripts
{
    [ModuleInitializer]
    internal static void RegisterTest1177Script() => Register(UpstreamTest1177Script.ScriptName, UpstreamTest1177Script.RunAsync);
}
