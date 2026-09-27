using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Works out the URL a <c>-T</c> / <c>--upload-file</c> transfer is sent to, as curl 8.21.0's
/// <c>setup_transfer_upload</c> does before it opens the upload source: the local file name is
/// appended by <see cref="UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile"/>, and the result is
/// parsed with <see cref="CurlUrl"/> and written back out normalised. Nothing here reads the file
/// system.
/// </summary>
public static class UploadTransferUrl
{
    /// <summary>
    /// curl 8.21.0's message when the URL of a <c>-T</c> upload cannot be parsed, measured on
    /// <c>-T nosuchfile "http://h/d ir/"</c> (exit 3). It differs from the transfer's own
    /// malformed-URL message.
    /// </summary>
    public const string MalformedUrlMessage = "URL using bad/illegal format or missing URL";

    /// <summary>
    /// Resolves the URL <paramref name="uploadFile"/> is uploaded to.
    /// </summary>
    /// <param name="url">The URL as typed.</param>
    /// <param name="uploadFile">The <c>-T</c> value paired with <paramref name="url"/>; not empty.</param>
    /// <param name="transferUrl">
    /// The URL to transfer: <paramref name="url"/> unchanged for an upload from standard input
    /// (<c>-</c> or <c>.</c>); otherwise the URL with the file name appended when its path names no
    /// file, normalised as curl writes it back: the scheme lower case and guessed (<c>http</c>) when
    /// absent, <c>http:/host</c> read as <c>http://host</c>, an absent path written as <c>/</c> and
    /// dot segments removed. The empty string when the URL cannot be parsed, which is what curl's
    /// <c>%{url_effective}</c> prints then.
    /// </param>
    /// <returns>
    /// <see langword="false"/> when <see cref="CurlUrl"/> rejects the URL, which curl reports with
    /// <see cref="CurlExitCode.UrlMalformat"/> and <see cref="MalformedUrlMessage"/> before the upload
    /// source is opened, even when nothing was appended.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="url"/> or <paramref name="uploadFile"/> is <see langword="null"/>.
    /// </exception>
    public static bool TryResolve(string url, string uploadFile, out string transferUrl)
    {
        if (UploadUrl.IsStandardInput(uploadFile))
        {
            ArgumentNullException.ThrowIfNull(url);
            transferUrl = url;
            return true;
        }

        string appended = UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile(url, uploadFile);
        if (!CurlUrl.TryParse(appended, pathAsIs: false, out CurlUrl? parsed))
        {
            transferUrl = string.Empty;
            return false;
        }

        transferUrl = Format(parsed);
        return true;
    }

    /// <summary>
    /// Writes <paramref name="url"/> back out from its parts, as curl's <c>curl_url_get</c> writes a
    /// whole URL. A port equal to the scheme's default is left out even when it was typed, which curl
    /// keeps; the host is written as <see cref="CurlUrl.Host"/> holds it.
    /// </summary>
    private static string Format(CurlUrl url)
    {
        StringBuilder text = new(url.Scheme);
        text.Append("://");
        AppendUserInformation(text, url);
        AppendHost(text, url);
        if (!url.IsDefaultPort)
        {
            text.Append(':').Append(url.Port);
        }

        if (!url.AbsolutePath.StartsWith('/'))
        {
            text.Append('/');
        }

        text.Append(url.AbsolutePath);
        if (url.Query is { } query)
        {
            text.Append('?').Append(query);
        }

        if (url.Fragment is { } fragment)
        {
            text.Append('#').Append(fragment);
        }

        return text.ToString();
    }

    private static void AppendUserInformation(StringBuilder text, CurlUrl url)
    {
        if (url.User is not { } user)
        {
            return;
        }

        text.Append(user);
        if (url.Password is { } password)
        {
            text.Append(':').Append(password);
        }

        if (url.Options is { } options)
        {
            text.Append(';').Append(options);
        }

        text.Append('@');
    }

    private static void AppendHost(StringBuilder text, CurlUrl url)
    {
        if (url.ZoneId is { } zoneId)
        {
            text.Append(url.Host.AsSpan(0, url.Host.Length - 1)).Append("%25").Append(zoneId).Append(']');
            return;
        }

        text.Append(url.Host);
    }
}
