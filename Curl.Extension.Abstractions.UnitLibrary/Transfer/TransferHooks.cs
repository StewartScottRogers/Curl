namespace Curl.Extension.Abstractions.Transfer;

/// <summary>
/// The transfer hooks of the extensions one option group activated (ADR-1007). A host builds it from
/// the group's <see cref="ExtensionOptionValues" /> and passes <see cref="Around" /> to the engine, which
/// puts every protocol handler through it; with no extension option given it holds no hook and gives
/// each handler back unchanged.
/// </summary>
/// <param name="hooks">The hooks, in the order they see the request.</param>
/// <param name="services">
/// What the host lends the hooks to make their own requests (ADR-1039), passed to each hook's
/// services overload of <see cref="ITransferHook.ExecuteAsync(ITransferContext, TransferHookServices, Func{ITransferContext, ValueTask{TransferResult}})" />;
/// <see langword="null" /> for none, when each hook's other overload runs.
/// </param>
public sealed class TransferHooks(IReadOnlyList<ITransferHook> hooks, TransferHookServices? services = null)
{
    /// <summary>Gets the hooks of an option group that activated no extension.</summary>
    public static TransferHooks None { get; } = new([]);

    /// <summary>Gets the hooks, in the order they see the request.</summary>
    public IReadOnlyList<ITransferHook> Hooks { get; } = hooks;

    /// <summary>Gives the hooks the extensions' activators return for <paramref name="values" />, in their order.</summary>
    /// <param name="values">The extension options the option group gave.</param>
    /// <param name="activators">One per extension: its hook when its option was given, otherwise <see langword="null" />.</param>
    /// <param name="services">What the host lends the hooks to make their own requests, or <see langword="null" /> for none.</param>
    /// <returns>The activated hooks.</returns>
    public static TransferHooks ActivatedBy(ExtensionOptionValues values, IEnumerable<Func<ExtensionOptionValues, ITransferHook?>> activators, TransferHookServices? services = null) =>
        new([.. activators.Select(activator => activator(values)).OfType<ITransferHook>()], services);

    /// <summary>Puts <paramref name="handler" />'s transfers through the hooks.</summary>
    /// <param name="handler">One of the engine's protocol handlers.</param>
    /// <returns><paramref name="handler" /> itself when there is no hook, otherwise a handler that runs the hooks around it.</returns>
    public IProtocolHandler Around(IProtocolHandler handler) =>
        Hooks.Count == 0 ? handler : new TransferHookingProtocolHandler(handler, Hooks, services);
}
