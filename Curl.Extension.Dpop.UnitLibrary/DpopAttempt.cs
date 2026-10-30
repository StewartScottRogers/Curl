namespace Curl.Extension.Dpop;

/// <summary>One try of a DPoP transfer: the response curl's handler received, held until the hook keeps it or retries.</summary>
/// <param name="dumpsHead">Whether the transfer dumps its headers (<c>-D</c>), so the dumped head is held too.</param>
internal sealed class DpopAttempt(bool dumpsHead)
{
    /// <summary>Gets the held body.</summary>
    public MemoryStream Body { get; } = new();

    /// <summary>Gets the held head, for <c>-i</c>.</summary>
    public MemoryStream Head { get; } = new();

    /// <summary>Gets the held dumped head, or <see langword="null" /> when the transfer dumps none.</summary>
    public MemoryStream? DumpedHead { get; } = dumpsHead ? new() : null;

    /// <summary>Gets or sets the handler's result.</summary>
    public TransferResult Result { get; set; } = TransferResult.Success(0);

    /// <summary>Gets or sets a value indicating whether the server refused the proof and gave a nonce it had not had (RFC 9449 §8, §9).</summary>
    public bool AsksForNewNonce { get; set; }

    /// <summary>Writes the held response to the transfer's own outputs.</summary>
    /// <param name="context">The transfer.</param>
    public async Task WriteToAsync(ITransferContext context)
    {
        await WriteIfGivenAsync(context.HeaderOutput, Head, context.CancellationToken);
        await WriteIfGivenAsync(context.DumpHeaderOutput, DumpedHead, context.CancellationToken);
        await context.Output.WriteAsync(Body.ToArray(), context.CancellationToken);
    }

    private static async Task WriteIfGivenAsync(Stream? output, MemoryStream? held, CancellationToken cancellationToken)
    {
        if (output is not null)
        {
            await output.WriteAsync(held!.ToArray(), cancellationToken);
        }
    }
}
