using System.Text;

namespace Curl.Extension.WebDavTools.UnitTests.Transfer;

/// <summary>
/// Stands in for curl's handler talking to a WebDAV server: it answers each request by its method and request path
/// (<c>PROPFIND /dav/</c>, <c>GET /dav/a.txt</c>) with the response set for it, and anything else with 404.
/// </summary>
internal sealed class DavOrigin
{
    /// <summary>Gets every request it received, in order.</summary>
    public List<ITransferContext> Requests { get; } = [];

    /// <summary>Gets the responses by method and request path.</summary>
    public Dictionary<string, (int Code, string Body)> Responses { get; } = [];

    /// <summary>Gets the transfers that fail before any response, by method and request path.</summary>
    public Dictionary<string, TransferResult> Failures { get; } = [];

    /// <summary>Gets the method and request path of each request, as <c>PROPFIND /dav/</c>.</summary>
    public IEnumerable<string> RequestLines => Requests.Select(KeyOf);

    public async ValueTask<TransferResult> Invoke(ITransferContext context)
    {
        Requests.Add(context);
        var key = KeyOf(context);
        if (Failures.TryGetValue(key, out var failure))
        {
            return failure;
        }

        var (code, body) = Responses.TryGetValue(key, out var response) ? response : (404, "");
        if (context.HeaderOutput is { } headers)
        {
            await headers.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 {code} X\r\n\r\n"));
        }

        var bytes = Encoding.UTF8.GetBytes(body);
        await context.Output.WriteAsync(bytes);
        return TransferResult.Success(bytes.Length) with { Report = new TransferReport { ResponseCode = code } };
    }

    /// <summary>Gives the value of the request header field <paramref name="name" /> the context carries.</summary>
    public static string? Field(ITransferContext context, string name) =>
        context.Http!.Headers.Select(line => line.Split(": ", 2)).FirstOrDefault(parts => parts[0] == name)?[1];

    /// <summary>Gives a <c>207 Multistatus</c> body holding <paramref name="responses" />.</summary>
    public static string Multistatus(params string[] responses) =>
        "<?xml version=\"1.0\" encoding=\"utf-8\"?><D:multistatus xmlns:D=\"DAV:\">" + string.Concat(responses) + "</D:multistatus>";

    /// <summary>Gives the <c>response</c> of a collection.</summary>
    public static string Collection(string href) =>
        $"<D:response><D:href>{href}</D:href><D:propstat><D:prop><D:resourcetype><D:collection/></D:resourcetype></D:prop><D:status>HTTP/1.1 200 OK</D:status></D:propstat></D:response>";

    /// <summary>Gives the <c>response</c> of a file of <paramref name="length" /> bytes, last modified at <paramref name="lastModified" /> when given.</summary>
    public static string File(string href, int length, string? lastModified) =>
        $"<D:response><D:href>{href}</D:href><D:propstat><D:prop><D:resourcetype/><D:getcontentlength>{length}</D:getcontentlength>"
        + (lastModified is null ? "" : $"<D:getlastmodified>{lastModified}</D:getlastmodified>")
        + "</D:prop><D:status>HTTP/1.1 200 OK</D:status></D:propstat></D:response>";

    private static string KeyOf(ITransferContext context) => context.Http!.CustomMethod + " " + context.Url.AbsolutePath;
}
