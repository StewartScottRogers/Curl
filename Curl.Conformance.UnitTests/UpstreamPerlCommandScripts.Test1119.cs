using System.Runtime.CompilerServices;

namespace Curl.Conformance;

internal static partial class UpstreamPerlCommandScripts
{
    [ModuleInitializer]
    internal static void RegisterTest1119Script() => Register(UpstreamTest1119Script.ScriptName, UpstreamTest1119Script.RunAsync);
}
