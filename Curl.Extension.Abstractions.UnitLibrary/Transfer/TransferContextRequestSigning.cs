namespace Curl.Extension.Abstractions.Transfer;

/// <summary>
/// Lets a hook that signs the transfer's request give a protocol handler a way to sign each further request it makes, so
/// that no request is sent with a signature naming another.
/// </summary>
public static class TransferContextRequestSigning
{
    /// <summary>Gives <paramref name="context" /> with <paramref name="signer" /> for the further requests its handler makes.</summary>
    /// <param name="context">The transfer the hook hands on.</param>
    /// <param name="signer">Signs one further request.</param>
    /// <returns>The same transfer carrying the signer.</returns>
    public static ITransferContext WithRequestSigner(this ITransferContext context, RequestHeadersSigner signer)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(signer);
        return new HookedTransferContext(context, context.Output, context.HeaderOutput, context.DumpHeaderOutput, context.Http) { Signer = signer };
    }

    /// <summary>Gives the headers one further request of the transfer is to carry.</summary>
    /// <param name="context">The transfer the handler was given.</param>
    /// <param name="method">The request's HTTP method.</param>
    /// <param name="url">The request's URL.</param>
    /// <param name="headers">The headers the transfer's own request carries, as <c>Name: value</c> lines.</param>
    /// <returns>
    /// <paramref name="headers" /> as every signer on <paramref name="context" /> and the contexts it wraps signs them;
    /// <paramref name="headers" /> itself when none is on them.
    /// </returns>
    public static IReadOnlyList<string> SignedHeadersFor(this ITransferContext context, string method, CurlUrl url, IReadOnlyList<string> headers)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(headers);
        for (var wrapper = context as IWrappingTransferContext; wrapper is not null; wrapper = wrapper.Inner as IWrappingTransferContext)
        {
            if (wrapper is HookedTransferContext { Signer: { } sign })
            {
                headers = sign(method, url, headers);
            }
        }

        return headers;
    }
}
