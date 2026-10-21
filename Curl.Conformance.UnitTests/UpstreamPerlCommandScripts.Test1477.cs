using System.Runtime.CompilerServices;

namespace Curl.Conformance;

internal static partial class UpstreamPerlCommandScripts
{
    [ModuleInitializer]
    internal static void RegisterTest1477Script() => Register(UpstreamTest1477Script.ScriptName, UpstreamTest1477Script.RunAsync);
}
