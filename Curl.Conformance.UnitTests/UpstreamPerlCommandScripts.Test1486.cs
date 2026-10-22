using System.Runtime.CompilerServices;

namespace Curl.Conformance;

internal static partial class UpstreamPerlCommandScripts
{
    [ModuleInitializer]
    internal static void RegisterTest1486Script() => Register(UpstreamTest1486Script.ScriptName, UpstreamTest1486Script.RunAsync);
}
