using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.ConnectOnly;
using Curl.Api.EngineContracts.Headers;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Info;
using Curl.Api.Options;

namespace Curl.Api.RequestBodies;

/// <summary>
/// An <see cref="IEasyTransfer"/> whose perform uploads the body as libcurl does: the post fields when
/// set, otherwise the read callback until it returns 0, <see cref="BufferSize"/> bytes at a time. With
/// <see cref="Passes"/> above 1 it rewinds through the seek callback before each further pass, as a
/// followed redirect does. A read that aborts ends with <see cref="CurlCode.AbortedByCallback"/>, a
/// seek that does not succeed with <see cref="CurlCode.SendFailRewind"/>.
/// </summary>
internal sealed class UploadingEasyTransfer : IEasyTransfer
{
    public EasyOptions Options { get; set; } = new();

    public TransferInfo Info { get; set; } = new();

    public string ErrorText { get; set; } = "";

    public int BufferSize { get; set; } = 4;

    public int Passes { get; set; } = 1;

    public EasyOptions? PerformedOptions { get; private set; }

    /// <summary>The bytes each pass uploaded.</summary>
    public List<byte[]> Uploads { get; } = [];

    public SeekResult? LastSeek { get; private set; }

    public ITransferHeaders Headers => throw new NotSupportedException();

    public IEasyWebSocket WebSocket => throw new NotSupportedException();

    public IConnectOnlyConnection Connection => throw new NotSupportedException();

    public ValueTask<CurlCode> PerformAsync(CancellationToken cancellationToken)
    {
        PerformedOptions = Options;
        return ValueTask.FromResult(Upload());
    }

    private CurlCode Upload()
    {
        for (int pass = 0; pass < Passes; pass++)
        {
            if (pass > 0 && (LastSeek = Options.Callbacks.Seek!(0, SeekOrigin.Begin)) != SeekResult.Ok)
            {
                return CurlCode.SendFailRewind;
            }

            if (!UploadOnce())
            {
                return CurlCode.AbortedByCallback;
            }
        }

        return CurlCode.Ok;
    }

    private bool UploadOnce()
    {
        if (Options.Http.PostFields is { } fields)
        {
            Uploads.Add(fields.ToArray());
            return true;
        }

        List<byte> sent = [];
        byte[] buffer = new byte[BufferSize];
        long read;
        while ((read = Options.Callbacks.Read!(buffer)) != 0)
        {
            if (read == ReadCallbackResult.Abort)
            {
                return false;
            }

            sent.AddRange(buffer.AsSpan(0, (int)read).ToArray());
        }

        Uploads.Add([.. sent]);
        return true;
    }

    public IEasyTransfer Duplicate() => throw new NotSupportedException();

    public void Reset() => throw new NotSupportedException();

    public CurlCode Pause(CurlPause directions) => CurlCode.Ok;

    public ValueTask<CurlCode> UpkeepAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
