using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Serves <paramref name="schemes" /> without building the handler that performs their
/// transfers until the first transfer needs it, so a run pays only for the handlers its URLs'
/// schemes use (BL-1715, AF-0063).
/// </summary>
/// <param name="schemes">The schemes the handler <paramref name="createHandler" /> builds serves.</param>
/// <param name="createHandler">Builds the handler; called at most once.</param>
internal sealed class LazyProtocolHandler(IReadOnlyCollection<string> schemes, Func<IProtocolHandler> createHandler) : IProtocolHandler
{
    private readonly Lazy<IProtocolHandler> handler = new(createHandler);

    /// <summary>Gets the schemes the built handler serves, without building it.</summary>
    public IReadOnlyCollection<string> SupportedSchemes => schemes;

    /// <summary>Gets whether the handler has been built.</summary>
    internal bool IsCreated => handler.IsValueCreated;

    /// <summary>Gets the handler, building it on first use.</summary>
    internal IProtocolHandler Handler => handler.Value;

    /// <summary>Performs the transfer with <see cref="Handler" />, building it on first use.</summary>
    /// <param name="context">The transfer.</param>
    /// <returns>The handler's result.</returns>
    public ValueTask<TransferResult> ExecuteAsync(ITransferContext context) => handler.Value.ExecuteAsync(context);
}
