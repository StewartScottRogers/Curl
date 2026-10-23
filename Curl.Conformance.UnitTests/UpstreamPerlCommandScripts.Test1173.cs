using System.Runtime.CompilerServices;

namespace Curl.Conformance;

internal static partial class UpstreamPerlCommandScripts
{
    [ModuleInitializer]
    internal static void RegisterTest1173Script() => Register(UpstreamTest1173Script.ScriptName, UpstreamTest1173Script.RunAsync);
}
