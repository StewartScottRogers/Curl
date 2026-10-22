using System.Runtime.CompilerServices;

namespace Curl.Conformance;

internal static partial class UpstreamPerlCommandScripts
{
    [ModuleInitializer]
    internal static void RegisterTest971Script() => Register(UpstreamTest971Script.ScriptName, UpstreamTest971Script.RunAsync);
}
