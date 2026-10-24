using System.Runtime.CompilerServices;

namespace Curl.Conformance;

internal static partial class UpstreamPerlCommandScripts
{
    [ModuleInitializer]
    internal static void RegisterTest1544Script() => Register(UpstreamTest1544Script.ScriptName, UpstreamTest1544Script.RunAsync);
}
