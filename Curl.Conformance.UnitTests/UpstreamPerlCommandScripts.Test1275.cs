using System.Runtime.CompilerServices;

namespace Curl.Conformance;

internal static partial class UpstreamPerlCommandScripts
{
    [ModuleInitializer]
    internal static void RegisterTest1275Script() => Register(UpstreamTest1275Script.ScriptName, UpstreamTest1275Script.RunAsync);
}
