using System.Text;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// Describes the <see cref="HttpRequestOptions" /> a request test sets, one short phrase per
/// option that differs from its default, for the test's <c>ARRANGE</c> diagnostic line.
/// </summary>
internal static class HttpRequestOptionsDescription
{
    /// <summary>Returns the options that differ from their defaults, or <c>(null)</c> or <c>(defaults)</c>.</summary>
    internal static string Of(HttpRequestOptions? options)
    {
        if (options is null)
        {
            return "(null)";
        }

        List<string> parts = [];
        AddIf(parts, options.CustomMethod is not null, $"method {options.CustomMethod}");
        AddIf(parts, options.Headers.Count > 0, $"headers [{string.Join(" | ", options.Headers)}]");
        AddIf(parts, options.ProxyHeaders.Count > 0, $"proxy headers [{string.Join(" | ", options.ProxyHeaders)}]");
        AddIf(parts, options.UserAgent is not null, $"user agent {options.UserAgent}");
        AddIf(parts, options.Referer is not null, $"referer {options.Referer}");
        AddIf(parts, options.Compressed, "compressed");
        AddIf(parts, options.TransferEncoding, "tr-encoding");
        AddIf(parts, options.Version != default, $"version {options.Version}");
        AddIf(parts, options.Body is not null, $"body {options.Body?.GetType().Name}");
        AddIf(parts, options.RequestTarget is not null, $"request target {options.RequestTarget}");
        AddIf(parts, options.AltSvcRoute is not null, "alt-svc route");
        AddIf(parts, options.CommandLineTextEncoding.CodePage != Encoding.Latin1.CodePage, $"text code page {options.CommandLineTextEncoding.CodePage}");
        return parts.Count == 0 ? "(defaults)" : string.Join(", ", parts);
    }

    private static void AddIf(List<string> parts, bool condition, string part)
    {
        if (condition)
        {
            parts.Add(part);
        }
    }
}
