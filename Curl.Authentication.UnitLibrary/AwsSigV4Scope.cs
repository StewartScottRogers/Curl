using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// The two providers, region and service an <c>--aws-sigv4</c> value names, completed from the
/// host name as curl 8.21.0 does.
/// </summary>
/// <param name="Provider0">The first provider, e.g. <c>aws</c>: it names the algorithm and the request type.</param>
/// <param name="Provider1">The second provider, e.g. <c>amz</c>: it names the <c>x-…-date</c> and content hash headers.</param>
/// <param name="Region">The region, e.g. <c>us-east-1</c>; empty when neither the value nor the host gave one.</param>
/// <param name="Service">The service, e.g. <c>s3</c>.</param>
internal sealed record AwsSigV4Scope(string Provider0, string Provider1, string Region, string Service)
{
    /// <summary>The longest provider, region or service curl accepts; a longer one reads as missing.</summary>
    private const int MaxPartLength = 64;

    /// <summary>The value curl uses when <c>--aws-sigv4</c> is empty.</summary>
    private const string DefaultParameter = "aws:amz";

    /// <summary>
    /// Gets the lines curl's <c>-v</c> writes when the service or region came from the host name,
    /// <c>aws_sigv4: picked service &lt;service&gt; from host</c> then
    /// <c>aws_sigv4: picked region &lt;region&gt; from host</c>; empty when the value named both.
    /// </summary>
    internal IReadOnlyList<string> PickedFromHostLines { get; init; } = [];

    /// <summary>
    /// Parses an <c>--aws-sigv4</c> value, taking the service and region from the host name's first
    /// two labels when the value does not name the service.
    /// </summary>
    /// <param name="parameter">The <c>--aws-sigv4</c> value.</param>
    /// <param name="hostName">The URL's host name.</param>
    /// <param name="failure">The failure curl reports, or <see langword="null" />.</param>
    /// <returns>The scope, or <see langword="null" /> on failure.</returns>
    internal static AwsSigV4Scope? Parse(string parameter, string hostName, out AwsSigV4SigningResult? failure)
    {
        string line = parameter.Length == 0 ? DefaultParameter : parameter;
        int position = 0;
        string provider0 = ReadUntil(line, ref position, ':');
        if (provider0.Length == 0)
        {
            failure = AwsSigV4SigningResult.Failed(CurlExitCode.BadFunctionArgument, "first aws-sigv4 provider cannot be empty");
            return null;
        }

        (string provider1, string region, string service) = ReadRest(line, position, provider0);
        return service.Length == 0
            ? FromHost(provider0, provider1, region, hostName, out failure)
            : Succeed(new AwsSigV4Scope(provider0, provider1, region, service), out failure);
    }

    private static (string Provider1, string Region, string Service) ReadRest(string line, int position, string provider0)
    {
        string provider1 = ReadSingle(line, ref position) ? ReadUntil(line, ref position, ':') : string.Empty;
        if (provider1.Length == 0)
        {
            return (provider0, string.Empty, string.Empty);
        }

        string region = ReadSingle(line, ref position) ? ReadUntil(line, ref position, ':') : string.Empty;
        string service = region.Length > 0 && ReadSingle(line, ref position) ? ReadUntil(line, ref position, ':') : string.Empty;
        return (provider1, region, service);
    }

    private static AwsSigV4Scope? FromHost(string provider0, string provider1, string region, string hostName, out AwsSigV4SigningResult? failure)
    {
        int position = 0;
        string service = ReadUntil(hostName, ref position, '.');
        if (service.Length == 0 || !ReadSingle(hostName, ref position, '.'))
        {
            failure = AwsSigV4SigningResult.Failed(CurlExitCode.UrlMalformat, "aws-sigv4: service missing in parameters and hostname");
            return null;
        }

        List<string> pickedFromHostLines = ["aws_sigv4: picked service " + service + " from host"];
        if (region.Length == 0)
        {
            region = ReadUntil(hostName, ref position, '.');
            if (region.Length == 0 || !ReadSingle(hostName, ref position, '.'))
            {
                failure = AwsSigV4SigningResult.Failed(CurlExitCode.UrlMalformat, "aws-sigv4: region missing in parameters and hostname");
                return null;
            }

            pickedFromHostLines.Add("aws_sigv4: picked region " + region + " from host");
        }

        return Succeed(new AwsSigV4Scope(provider0, provider1, region, service) { PickedFromHostLines = pickedFromHostLines }, out failure);
    }

    private static AwsSigV4Scope Succeed(AwsSigV4Scope scope, out AwsSigV4SigningResult? failure)
    {
        failure = null;
        return scope;
    }

    /// <summary>
    /// Reads up to <paramref name="delimiter" /> or the end, as curl's <c>curlx_str_until</c>:
    /// an empty or over-long part reads as empty and leaves the position where it was.
    /// </summary>
    private static string ReadUntil(string text, ref int position, char delimiter)
    {
        int end = text.IndexOf(delimiter, position);
        int length = (end < 0 ? text.Length : end) - position;
        if (length is 0 or > MaxPartLength)
        {
            return string.Empty;
        }

        string part = text.Substring(position, length);
        position += length;
        return part;
    }

    /// <summary>Consumes <paramref name="separator" /> when it is next, as curl's <c>curlx_str_single</c>.</summary>
    private static bool ReadSingle(string text, ref int position, char separator = ':')
    {
        if (position < text.Length && text[position] == separator)
        {
            position++;
            return true;
        }

        return false;
    }
}
