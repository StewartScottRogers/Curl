using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.Abstractions.Scheme;

/// <summary>
/// The scheme hook of the extension one option group activated (ADR-1048). A host builds it from the group's
/// <see cref="ExtensionOptionValues" /> and puts each of curl's handlers through <see cref="Over" />; with no
/// extension option given it holds no hook and gives curl's handlers back unchanged.
/// </summary>
public sealed class SchemeHooks
{
    private readonly TransferHookServices? services;

    private SchemeHooks(ISchemeHook? hook, TransferHookServices? services)
    {
        Hook = hook;
        this.services = services;
    }

    /// <summary>Gets the hooks of an option group that activated no extension.</summary>
    public static SchemeHooks None { get; } = new(null, null);

    /// <summary>Gets the activated hook, or <see langword="null" /> when none was.</summary>
    public ISchemeHook? Hook { get; }

    /// <summary>Gives the hook the extensions' activators return for <paramref name="values" />.</summary>
    /// <param name="values">The extension options the option group gave.</param>
    /// <param name="activators">One per extension: its hook when its option was given, otherwise <see langword="null" />.</param>
    /// <param name="services">What the host lends the hook's handler (ADR-1039).</param>
    /// <returns>The activated hook, or <see cref="None" />.</returns>
    /// <exception cref="ArgumentException">Two or more extensions' options claim curl's schemes; the message names each option.</exception>
    public static SchemeHooks ActivatedBy(ExtensionOptionValues values, IEnumerable<Func<ExtensionOptionValues, ISchemeHook?>> activators, TransferHookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        ISchemeHook[] hooks = [.. activators.Select(activator => activator(values)).OfType<ISchemeHook>()];
        return hooks.Length > 1
            ? throw new ArgumentException($"the options {string.Join(" and ", hooks.Select(hook => $"--{hook.Option.Name}"))} each serve a curl scheme in place of curl's handler; give only one of them", nameof(values))
            : new SchemeHooks(hooks.SingleOrDefault(), services);
    }

    /// <summary>Gives the handler a transfer for one of <paramref name="curlHandler" />'s schemes runs through.</summary>
    /// <param name="curlHandler">One of curl's handlers.</param>
    /// <returns>
    /// <paramref name="curlHandler" /> itself without a hook or when it serves none of the hook's schemes, otherwise a
    /// handler that serves the hook's schemes with the hook's handler and the rest with <paramref name="curlHandler" />.
    /// </returns>
    public IProtocolHandler Over(IProtocolHandler curlHandler) =>
        Hook is null || !curlHandler.SupportedSchemes.Any(Hook.Schemes.Contains)
            ? curlHandler
            : new SchemeServingProtocolHandler(curlHandler, Hook.Schemes, Hook.HandlerOver(curlHandler, services!));
}
