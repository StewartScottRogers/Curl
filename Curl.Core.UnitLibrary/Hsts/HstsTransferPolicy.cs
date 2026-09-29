using System.Diagnostics.CodeAnalysis;
using Curl.Protocol.Abstractions;

namespace Curl.Core.Hsts;

/// <summary>
/// Applies one run's <see cref="HstsCache" /> to its transfers as curl 8.21.0 does: an <c>http</c>
/// URL to a known host is switched to <c>https</c> before it is connected to, and every
/// <c>Strict-Transport-Security</c> header of a response that came over <c>https</c> is learned
/// (measured 2026-09-29, BL-621 Notes; ADR-0218).
/// </summary>
/// <remarks>
/// The switch changes the scheme only: the rest of the URL is kept as typed, so an explicit
/// <c>:80</c> stays (<c>http://localhost:80/</c> becomes <c>https://localhost:80/</c>), while a URL
/// without a port moves to 443 with the scheme's default. A header received over plain <c>http</c>
/// is ignored. Every member takes one lock, so <c>-Z</c> transfers may share the cache.
/// </remarks>
/// <param name="timeProvider">The clock expiries are counted on.</param>
public sealed class HstsTransferPolicy(TimeProvider timeProvider)
{
    /// <summary>
    /// What curl's <c>-v</c> line says before the switched URL: <c>Switched from HTTP to HTTPS due to
    /// HSTS =&gt; https://localhost:18443/</c>.
    /// </summary>
    public const string SwitchedMessagePrefix = "Switched from HTTP to HTTPS due to HSTS => ";

    private const string HeaderName = "Strict-Transport-Security";

    private readonly HstsCache cache = new(timeProvider);

    private readonly Lock gate = new();

    /// <summary>Adds the entries of an HSTS file's text, as <see cref="HstsCache.ReadFile" /> does.</summary>
    /// <param name="fileText">The whole file.</param>
    public void ReadFile(string fileText)
    {
        lock (gate)
        {
            cache.ReadFile(fileText);
        }
    }

    /// <summary>Writes the file's text, as <see cref="HstsCache.FormatFile" /> does.</summary>
    /// <param name="lineEnding">What ends each line.</param>
    /// <param name="latestWritableExpiry">The latest expiry the platform's curl can write.</param>
    /// <returns>The file's text, or <see langword="null" /> when curl would leave the file as it was.</returns>
    public string? FormatFile(string lineEnding, long latestWritableExpiry)
    {
        lock (gate)
        {
            return cache.FormatFile(lineEnding, latestWritableExpiry);
        }
    }

    /// <summary>
    /// Switches an <c>http</c> URL whose host the cache knows to <c>https</c>.
    /// </summary>
    /// <param name="urlText">The URL's text, written with its scheme, such as <c>HTTP://Host:80/a</c>.</param>
    /// <param name="url"><paramref name="urlText" />, parsed.</param>
    /// <param name="httpsUrl">
    /// <paramref name="urlText" /> with its scheme replaced by <c>https</c> when switched.
    /// </param>
    /// <returns>Whether the URL is switched.</returns>
    public bool TrySwitchToHttps(string urlText, CurlUrl url, [NotNullWhen(true)] out string? httpsUrl)
    {
        ArgumentNullException.ThrowIfNull(urlText);
        ArgumentNullException.ThrowIfNull(url);

        httpsUrl = url.Scheme == "http" && Knows(HostName(url))
            ? "https" + urlText[urlText.IndexOf(':', StringComparison.Ordinal)..]
            : null;
        return httpsUrl is not null;
    }

    /// <summary>
    /// Learns each <c>Strict-Transport-Security</c> header of <paramref name="report" />, in the order
    /// received, when <paramref name="url" /> is <c>https</c>.
    /// </summary>
    /// <param name="url">The URL the response came from.</param>
    /// <param name="report">The response's report; <see langword="null" /> when there was none.</param>
    public void LearnFrom(CurlUrl url, TransferReport? report)
    {
        ArgumentNullException.ThrowIfNull(url);

        if (url.Scheme != "https" || report is null)
        {
            return;
        }

        lock (gate)
        {
            foreach (KeyValuePair<string, string> header in report.ResponseHeaders)
            {
                if (string.Equals(header.Key, HeaderName, StringComparison.OrdinalIgnoreCase))
                {
                    cache.ApplyHeader(header.Value, HostName(url));
                }
            }
        }
    }

    /// <summary>The host as curl's connection holds it: punycode for a name, an IPv6 address without brackets.</summary>
    private static string HostName(CurlUrl url) =>
        url.Host.StartsWith('[') ? url.Host[1..^1] : url.IdnHost;

    private bool Knows(string host)
    {
        lock (gate)
        {
            return cache.Find(host) is not null;
        }
    }
}
