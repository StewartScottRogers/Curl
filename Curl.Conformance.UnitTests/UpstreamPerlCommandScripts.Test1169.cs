using System.Runtime.CompilerServices;

namespace Curl.Conformance;

internal static partial class UpstreamPerlCommandScripts
{
    [ModuleInitializer]
    internal static void RegisterTest1169Script() => Register(UpstreamRunTestsScript.ScriptName, UpstreamRunTestsScript.RunAsync);
}
