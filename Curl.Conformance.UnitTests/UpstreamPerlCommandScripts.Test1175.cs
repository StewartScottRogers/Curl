using System.Runtime.CompilerServices;

namespace Curl.Conformance;

internal static partial class UpstreamPerlCommandScripts
{
    [ModuleInitializer]
    internal static void RegisterTest1175Script() => Register(UpstreamTest1175Script.ScriptName, UpstreamTest1175Script.RunAsync);
}
