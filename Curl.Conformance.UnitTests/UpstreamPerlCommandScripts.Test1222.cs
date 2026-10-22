using System.Runtime.CompilerServices;

namespace Curl.Conformance;

internal static partial class UpstreamPerlCommandScripts
{
    [ModuleInitializer]
    internal static void RegisterTest1222Script() => Register(UpstreamTest1222Script.ScriptName, UpstreamTest1222Script.RunAsync);
}
