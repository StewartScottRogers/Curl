using System.Runtime.CompilerServices;

namespace Curl.Conformance;

internal static partial class UpstreamPerlCommandScripts
{
    [ModuleInitializer]
    internal static void RegisterTest1488Script() => Register(UpstreamTest1488Script.ScriptName, UpstreamTest1488Script.RunAsync);
}
