namespace Curl.Conformance;

/// <summary>
/// The Perl scripts a case's <c>&lt;command type="perl"&gt;</c> runs, each interpreted in C# (BL-2887, BL-2865) and
/// given to the ratchet's runner. Each script registers itself from its own
/// <c>UpstreamPerlCommandScripts.Test&lt;N&gt;.cs</c> file through a module initializer, so adding one touches only
/// that file (BL-2994).
/// </summary>
internal static partial class UpstreamPerlCommandScripts
{
    private static readonly Dictionary<string, Func<UpstreamCurlInvocation, Func<UpstreamCurlInvocation, Task<int>>, Task<int>>> Registered = [];

    /// <summary>Every registered script by its name.</summary>
    internal static IReadOnlyDictionary<string, Func<UpstreamCurlInvocation, Func<UpstreamCurlInvocation, Task<int>>, Task<int>>> All => Registered;

    private static void Register(string scriptName, Func<UpstreamCurlInvocation, Func<UpstreamCurlInvocation, Task<int>>, Task<int>> run) =>
        Registered.Add(scriptName, run);
}
