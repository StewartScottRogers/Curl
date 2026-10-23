using System.Runtime.CompilerServices;

namespace Curl.Conformance;

internal static partial class UpstreamPerlCommandScripts
{
    [ModuleInitializer]
    internal static void RegisterTest1140Script() => Register(UpstreamTest1140Script.ScriptName, UpstreamTest1140Script.RunAsync);
}
