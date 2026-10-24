using System.Runtime.CompilerServices;

namespace Curl.Conformance;

internal static partial class UpstreamPerlCommandScripts
{
    [ModuleInitializer]
    internal static void RegisterTest1276Script() => Register(UpstreamTest1276Script.ScriptName, UpstreamTest1276Script.RunAsync);
}
