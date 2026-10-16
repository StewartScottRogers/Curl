using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.Options.Http;
using Curl.Core.Multipart;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// The engine's deprecated form API (ADR-0506, ADR-0618): <see cref="Add" /> is curl 8.22.0's <c>curl_formadd</c>,
/// reading each call's <c>CURLFORM_*</c> options with <see cref="FormAddCall" /> and checking them with
/// <see cref="FormPost.Complete" />, <see cref="Build" /> the <see cref="EasyMimeBody" /> <c>Curl_getformdata</c> makes of
/// the fields, and <see cref="Serialize" /> <c>curl_formget</c>; dropping the builder is <c>curl_formfree</c>.
/// </summary>
/// <param name="fileSystem">Opens the files <see cref="Serialize" /> reads.</param>
/// <param name="createBoundary">Makes each multipart's boundary when <see cref="Serialize" /> runs.</param>
internal sealed class ManagedFormBuilder(IFileSystem fileSystem, Func<string> createBoundary) : ICurlFormBuilder
{
    /// <summary>The most bytes <see cref="Serialize" /> hands over at once: <c>curl_formget</c>'s buffer.</summary>
    internal const int PieceSize = 8192;

    private readonly List<FormPost> posts = [];

    /// <summary>
    /// <c>curl_formadd</c>: reads <paramref name="arguments" /> and, when every option and the field they describe
    /// are accepted, adds the field after the ones before.
    /// </summary>
    /// <param name="arguments">The field's options in order.</param>
    /// <returns>The call's result; nothing is added when it fails.</returns>
    public CurlFormAddCode Add(IReadOnlyList<CurlFormArgument> arguments)
    {
        CurlFormAddCode code = FormAddCall.Read(arguments, out FormAddCall call);
        FormPost? post = null;
        if (code == CurlFormAddCode.Ok)
        {
            code = FormPost.Complete(call, out post);
        }

        if (post is not null)
        {
            posts.Add(post);
        }

        return code;
    }

    /// <inheritdoc />
    public EasyMimeBody Build() => new() { Parts = [.. posts.Select(post => post.ToMimePart())] };

    /// <summary>
    /// <c>curl_formget</c>: hands <paramref name="append" /> the form as <c>curl_formget</c> reads its top part - the
    /// <c>Content-Type: multipart/form-data; boundary=...</c> line, an empty line and the multipart body - in pieces of
    /// at most <see cref="PieceSize" /> bytes, each as full as the form allows. <c>curl_formget</c> is synchronous, so
    /// this blocks on the asynchronous build and reads, as <c>CurlEasy.Perform</c> blocks on <c>PerformAsync</c>.
    /// </summary>
    /// <param name="append">Receives each piece.</param>
    /// <returns>
    /// 0 once the whole form was handed over; 26 (<c>CURLE_READ_ERROR</c>) when a file cannot be read or
    /// <paramref name="append" /> takes fewer bytes than it was given, or the build's other <c>CURLcode</c>.
    /// </returns>
    public int Serialize(FormGetCallback append) => SerializeAsync(append).AsTask().GetAwaiter().GetResult();

    private async ValueTask<int> SerializeAsync(FormGetCallback append)
    {
        MultipartFormBodyBuilder builder = new(fileSystem, Encoding.UTF8, createBoundary);
        MultipartFormBuildResult form = await builder.BuildAsync(MultipartFormPartMapping.FromMimeBody(Build()), CancellationToken.None).ConfigureAwait(false);
        if (!form.IsBuilt)
        {
            return (int)form.Failure.ExitCode;
        }

        await using Stream body = form.Body.Content;
        byte[] head = Encoding.UTF8.GetBytes($"Content-Type: {form.Body.ContentType}\r\n\r\n");
        byte[] buffer = new byte[PieceSize];
        head.CopyTo(buffer, 0);
        for (int filled = await FillAsync(body, buffer, head.Length).ConfigureAwait(false); filled > 0; filled = await FillAsync(body, buffer, 0).ConfigureAwait(false))
        {
            if (append(buffer.AsSpan(0, filled)) != filled)
            {
                return (int)CurlCode.ReadError;
            }
        }

        return (int)CurlCode.Ok;
    }

    /// <summary>Reads into <paramref name="buffer" /> after its first <paramref name="filled" /> bytes until it is full or <paramref name="content" /> ends.</summary>
    /// <returns>The bytes the buffer holds, 0 at the end.</returns>
    private static async ValueTask<int> FillAsync(Stream content, byte[] buffer, int filled)
    {
        for (int read = -1; read != 0 && filled < buffer.Length; filled += read)
        {
            read = await content.ReadAsync(buffer.AsMemory(filled)).ConfigureAwait(false);
        }

        return filled;
    }
}
