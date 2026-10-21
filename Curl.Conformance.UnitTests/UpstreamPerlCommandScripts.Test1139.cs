using System.Runtime.CompilerServices;

namespace Curl.Conformance;

internal static partial class UpstreamPerlCommandScripts
{
    [ModuleInitializer]
    internal static void RegisterTest1139Script() => Register(UpstreamTest1139Script.ScriptName, UpstreamTest1139Script.RunAsync);
}
