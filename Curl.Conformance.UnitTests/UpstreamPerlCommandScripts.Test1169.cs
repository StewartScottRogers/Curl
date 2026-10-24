using System.Runtime.CompilerServices;

namespace Curl.Conformance;

internal static partial class UpstreamPerlCommandScripts
{
    [ModuleInitializer]
    internal static void RegisterTest1169Script() => Register(UpstreamTest1169Script.ScriptName, UpstreamTest1169Script.RunAsync);
}
