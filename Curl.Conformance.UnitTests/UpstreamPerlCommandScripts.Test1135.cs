using System.Runtime.CompilerServices;

namespace Curl.Conformance;

internal static partial class UpstreamPerlCommandScripts
{
    [ModuleInitializer]
    internal static void RegisterTest1135Script() => Register(UpstreamTest1135Script.ScriptName, UpstreamTest1135Script.RunAsync);
}
