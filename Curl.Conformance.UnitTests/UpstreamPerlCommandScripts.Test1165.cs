using System.Runtime.CompilerServices;

namespace Curl.Conformance;

internal static partial class UpstreamPerlCommandScripts
{
    [ModuleInitializer]
    internal static void RegisterTest1165Script() => Register(UpstreamTest1165Script.ScriptName, UpstreamTest1165Script.RunAsync);
}
