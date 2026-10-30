using Curl.Protocol.Abstractions;

namespace Curl.Extension.ResumableUpload.Transfer;

/// <summary>
/// Creates an upload, appends its content to the upload resource the creation's <c>Location</c> names, and after a
/// broken connection retrieves the server's offset and appends from there, resuming at most a fixed number of times.
/// </summary>
/// <param name="protocol">What tus or the IETF draft sends at each step.</param>
/// <param name="session">Sends the requests.</param>
/// <param name="maxResumptions">How many times a broken step is tried again.</param>
internal sealed class ResumableUploadFlow(IResumableUploadProtocol protocol, ResumableUploadSession session, int maxResumptions)
{
    /// <summary>Uploads <paramref name="body" /> to <paramref name="target" />.</summary>
    /// <param name="target">The transfer's URL.</param>
    /// <param name="body">The whole upload, seekable.</param>
    /// <returns>The last exchange: the append's, or a creation or offset retrieval answered with a failure.</returns>
    /// <exception cref="ResumableUploadTransferException">A step failed for good.</exception>
    public async Task<ResumableUploadExchange> UploadAsync(Uri target, Stream body)
    {
        var creation = await ResumeAsync(_ => session.SendAsync(protocol.CreationMethod, target, protocol.CreationHeaders(body.Length), Stream.Null));
        if (!creation.IsSuccess || creation.FindHeader("Location") is not { } location)
        {
            return creation;
        }

        var upload = new Uri(target, location);
        long offset = 0;
        return await ResumeAsync(async resuming =>
        {
            if (resuming)
            {
                var retrieval = await session.SendAsync("HEAD", upload, protocol.OffsetRetrievalHeaders);
                if (!retrieval.IsSuccess)
                {
                    return retrieval;
                }

                offset = OffsetIn(retrieval, body.Length);
            }

            return await session.SendAsync("PATCH", upload, protocol.AppendHeaders(offset), body, offset);
        });
    }

    private long OffsetIn(ResumableUploadExchange retrieval, long length) =>
        protocol.TryReadOffset(retrieval, out var offset) && offset <= length
            ? offset
            : throw new ResumableUploadTransferException(
                CurlExitCode.WeirdServerReply, $"The offset retrieval response gave no Upload-Offset within the upload's {length} bytes.", canResume: false);

    private async Task<ResumableUploadExchange> ResumeAsync(Func<bool, Task<ResumableUploadExchange>> step)
    {
        for (var resumptions = 0; ; resumptions++)
        {
            try
            {
                return await step(resumptions > 0);
            }
            catch (ResumableUploadTransferException exception) when (exception.CanResume && resumptions < maxResumptions)
            {
            }
        }
    }
}
