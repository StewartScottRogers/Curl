using Curl.Extension.Protocol.Coap.Client;
using Curl.Extension.Protocol.Coap.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.Coap.Blockwise;

/// <summary>
/// Carries a request whose payload or response is larger than one block across several exchanges
/// (RFC 7959, ADR-1057). A payload larger than 1024 bytes (SZX 6) goes out in Block1 blocks, the next
/// after each 2.31 Continue, at the smaller size a 2.31's Block1 asks for; a 4.13 Request Entity Too
/// Large whose Block1 asks for a smaller size starts the payload again at that size. A response with
/// Block2 is then fetched block after block, at the size the server chooses, by repeating the request
/// without payload with Block2 naming the next block, and given as one response whose payload is every
/// block once, in order. Each message takes the next Message ID; all keep the request's token.
/// </summary>
/// <param name="exchange">Sends one confirmable message and gives its response.</param>
internal sealed class CoapBlockwiseTransfer(Func<CoapMessage, CancellationToken, ValueTask<CoapMessage>> exchange)
{
    /// <summary>The size exponent the first block uses: 1024 bytes.</summary>
    public const int DefaultSizeExponent = CoapBlockValue.MaximumSizeExponent;

    private ushort messageId;

    /// <summary>Sends <paramref name="request" /> block-wise as its payload needs and gives its whole response.</summary>
    /// <param name="request">The request, with its whole payload.</param>
    /// <param name="cancellationToken">Cancels the transfer.</param>
    /// <returns>The final response, its payload every Block2 block joined.</returns>
    /// <exception cref="CoapException">The server sent a block out of order, a short block or an invalid block option (exit 56), or an exchange failed.</exception>
    public async ValueTask<CoapMessage> RunAsync(CoapMessage request, CancellationToken cancellationToken)
    {
        messageId = request.MessageId;
        CoapMessage response = await SendPayloadAsync(request, cancellationToken).ConfigureAwait(false);
        return await ReceivePayloadAsync(request, response, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<CoapMessage> SendPayloadAsync(CoapMessage request, CancellationToken stop)
    {
        byte[] payload = request.Payload;
        int sizeExponent = DefaultSizeExponent;
        int offset = 0;
        while (true)
        {
            int size = SizeOf(sizeExponent);
            int length = Math.Min(size, payload.Length - offset);
            bool more = offset + length < payload.Length;
            CoapMessage response = await ExchangeAsync(request, Block1Options(offset, size, more, sizeExponent), payload[offset..(offset + length)], stop).ConfigureAwait(false);
            int asked = BlockOf(response, CoapOptionNumber.Block1)?.SizeExponent ?? sizeExponent;
            if (response.Code == CoapCode.RequestEntityTooLarge && asked < sizeExponent)
            {
                sizeExponent = asked;
                offset = 0;
                continue;
            }

            if (!more || response.Code != CoapCode.Continue)
            {
                return response;
            }

            offset += length;
            sizeExponent = Math.Min(sizeExponent, asked);
        }
    }

    /// <summary>Gives no Block1 option for a payload that fits one block whole, else the Block1 option for the block at <paramref name="offset" />.</summary>
    private static CoapOption[] Block1Options(int offset, int size, bool more, int sizeExponent) =>
        offset == 0 && !more ? [] : [BlockOption(CoapOptionNumber.Block1, offset / size, more, sizeExponent)];

    private async ValueTask<CoapMessage> ReceivePayloadAsync(CoapMessage request, CoapMessage response, CancellationToken stop)
    {
        var body = new MemoryStream();
        while (BlockOf(response, CoapOptionNumber.Block2) is { } block)
        {
            Append(body, block, response.Payload);
            if (!block.More)
            {
                return response with { Payload = body.ToArray() };
            }

            CoapOption block2 = BlockOption(CoapOptionNumber.Block2, (int)(body.Length / block.Size), false, block.SizeExponent);
            response = await ExchangeAsync(request, [block2], [], stop).ConfigureAwait(false);
        }

        return response;
    }

    /// <summary>Writes the part of a block's payload not yet in <paramref name="body" />, so a repeated block is written once.</summary>
    private static void Append(MemoryStream body, CoapBlockValue block, byte[] payload)
    {
        long offset = (long)block.Number * block.Size;
        if (offset > body.Length || (block.More && payload.Length != block.Size))
        {
            throw new CoapException(CurlExitCode.RecvError, $"CoAP server sent block {block.Number} out of order or short");
        }

        int written = (int)(body.Length - offset);
        body.Write(payload, Math.Min(written, payload.Length), Math.Max(payload.Length - written, 0));
    }

    private ValueTask<CoapMessage> ExchangeAsync(CoapMessage request, CoapOption[] blockOptions, byte[] payload, CancellationToken stop) =>
        exchange(request with { MessageId = messageId++, Options = [.. request.Options, .. blockOptions], Payload = payload }, stop);

    private static CoapBlockValue? BlockOf(CoapMessage response, int number)
    {
        CoapOption? option = response.Options.FirstOrDefault(candidate => candidate.Number == number);
        try
        {
            return option is null ? null : CoapBlockValue.Decode(option.Value);
        }
        catch (FormatException)
        {
            throw new CoapException(CurlExitCode.RecvError, "CoAP server sent an invalid block option");
        }
    }

    private static CoapOption BlockOption(int number, int blockNumber, bool more, int sizeExponent) =>
        new(number, new CoapBlockValue((uint)blockNumber, more, sizeExponent).Encode());

    private static int SizeOf(int sizeExponent) => 1 << (sizeExponent + 4);
}
