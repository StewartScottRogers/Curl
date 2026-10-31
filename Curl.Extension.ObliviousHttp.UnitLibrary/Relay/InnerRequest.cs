using System.Text;
using Curl.Extension.ObliviousHttp.BinaryHttp;

namespace Curl.Extension.ObliviousHttp.Relay;

/// <summary>
/// The request the transfer's options describe, as the Binary HTTP request (RFC 9292 section 3) the hook
/// encapsulates: its method, the target URL's scheme, authority and path, the <c>-H</c> header lines (with
/// <c>-A</c>'s User-Agent and <c>-d</c>'s form Content-Type where no line gives them) and the <c>-d</c> body.
/// </summary>
internal static class InnerRequest
{
    private const string FormContentType = "application/x-www-form-urlencoded";

    /// <summary>Refuses a transfer whose request the hook cannot carry inside the encapsulated one.</summary>
    /// <exception cref="ObliviousTransferException">An upload, a HEAD, a range or resume, a time condition or a form: exit 2.</exception>
    public static void ThrowIfNotRelayable(ITransferContext context)
    {
        if (RefusalOf(context) is { } refused)
        {
            throw ObliviousTransferException.Failing(CurlExitCode.FailedInit, $"--{ObliviousHttpExtension.RelayOption.Name} cannot send {refused}");
        }
    }

    /// <summary>The Binary HTTP request for <paramref name="context" />'s HTTP transfer.</summary>
    public static BinaryHttpRequest Of(ITransferContext context)
    {
        HttpRequestOptions http = context.Http!;
        List<BinaryHttpField> headers = [.. http.Headers.Select(FieldOf).OfType<BinaryHttpField>()];
        AddUnlessGiven(headers, "user-agent", http.UserAgent);
        AddUnlessGiven(headers, "content-type", context.PostData is null ? null : FormContentType);
        AddUnlessGiven(headers, "authorization", AuthorizationOf(context));
        AddUnlessGiven(headers, "referer", http.Referer);
        AddUnlessGiven(headers, "cookie", http.CookieStrings);
        return new BinaryHttpRequest(MethodOf(context), context.Url.Scheme, AuthorityOf(context.Url), PathOf(context.Url, http), headers, context.PostData ?? ReadOnlyMemory<byte>.Empty);
    }

    /// <summary>The URL's host, with its port when it is not the scheme's default.</summary>
    public static string AuthorityOf(CurlUrl url) => url.IsDefaultPort ? url.Host : url.Host + ":" + url.Port;

    /// <summary>Each request the hook cannot carry, in the order it checks them, with the words its refusal names it by.</summary>
    private static readonly (Func<ITransferContext, bool> Applies, string Refused)[] Refusals =
    [
        (context => context.Upload is not null, "an upload (-T)"),
        (context => context.NoBody, "a HEAD request (-I)"),
        (context => context.Range is not null || context.ResumeFrom is not null, "a range or resumed transfer (-r, -C)"),
        (context => context.TimeCondition is not null, "a time condition (-z)"),
        (context => context.Http!.Body is not null, "a form (-F)"),
        (context => context.Http!.Compressed, "a compressed response (--compressed)"),
        (context => context.Http!.HasCookieFiles, "a cookie file (-b)"),
    ];

    /// <summary>Bearer token (<c>--oauth2-bearer</c>) first, else Basic credentials (<c>-u</c>), as the <c>Authorization</c> value.</summary>
    private static string? AuthorizationOf(ITransferContext context) =>
        context.Http!.BearerToken is { } token
            ? "Bearer " + token
            : context.Credentials is { } credentials
                ? "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials.UserName + ":" + credentials.Password))
                : null;

    private static string? RefusalOf(ITransferContext context) =>
        Array.Find(Refusals, refusal => refusal.Applies(context)).Refused;

    private static string MethodOf(ITransferContext context) =>
        context.Http!.CustomMethod ?? (context.PostData is not null || context.PostWithoutData ? "POST" : "GET");

    private static string PathOf(CurlUrl url, HttpRequestOptions http) =>
        http.RequestTarget ?? (url.Query is null ? url.AbsolutePath : url.AbsolutePath + "?" + url.Query);

    /// <summary>
    /// The field a <c>-H</c> line sends: <c>Name: value</c>, or <c>Name;</c> for an empty value. A line
    /// <c>Name:</c> with no value removes curl's own header and sends none, as curl does.
    /// </summary>
    private static BinaryHttpField? FieldOf(string line)
    {
        int colon = line.IndexOf(':', StringComparison.Ordinal);
        if (colon > 0)
        {
            string value = line[(colon + 1)..].Trim();
            return value.Length == 0 ? null : new BinaryHttpField(line[..colon].Trim(), value);
        }

        return line.EndsWith(';') && line.Length > 1 ? new BinaryHttpField(line[..^1].Trim(), string.Empty) : null;
    }

    private static void AddUnlessGiven(List<BinaryHttpField> headers, string name, string? value)
    {
        if (value is not null && !headers.Exists(field => string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            headers.Add(new BinaryHttpField(name, value));
        }
    }
}
