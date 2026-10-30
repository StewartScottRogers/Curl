namespace Curl.Extension.Abstractions.Resolution;

/// <summary>
/// The resolver hook of the extension one option group activated (ADR-1012). A host builds it from the
/// group's <see cref="ExtensionOptionValues" /> and whether the group gave <c>--doh-url</c>, and passes
/// <see cref="ResolverOver" /> to the engine; with no extension option given it holds no hook and gives
/// curl's resolver back unchanged.
/// </summary>
public sealed class ResolverHooks
{
    private ResolverHooks(IResolverHook? hook) => Hook = hook;

    /// <summary>Gets the hooks of an option group that activated no extension.</summary>
    public static ResolverHooks None { get; } = new(null);

    /// <summary>Gets the activated hook, or <see langword="null" /> when none was.</summary>
    public IResolverHook? Hook { get; }

    /// <summary>Gives the hook the extensions' activators return for <paramref name="values" />.</summary>
    /// <param name="values">The extension options the option group gave.</param>
    /// <param name="activators">One per extension: its hook when its option was given, otherwise <see langword="null" />.</param>
    /// <param name="dohUrlGiven">Whether the option group gave curl's <c>--doh-url</c>, which also supplies the resolver.</param>
    /// <returns>The activated hook, or <see cref="None" />.</returns>
    /// <exception cref="ExtensionHookConflictException">Two or more of the extensions' options and <c>--doh-url</c> claim the transfer's resolver; the message names each option.</exception>
    public static ResolverHooks ActivatedBy(ExtensionOptionValues values, IEnumerable<Func<ExtensionOptionValues, IResolverHook?>> activators, bool dohUrlGiven)
    {
        IResolverHook[] hooks = [.. activators.Select(activator => activator(values)).OfType<IResolverHook>()];
        string[] claimants = [.. hooks.Select(hook => $"--{hook.Option.Name}"), .. dohUrlGiven ? (string[])["--doh-url"] : []];
        return claimants.Length > 1
            ? throw new ExtensionHookConflictException($"the options {string.Join(" and ", claimants)} each supply the resolver a transfer looks host names up with; give only one of them")
            : new ResolverHooks(hooks.SingleOrDefault());
    }

    /// <summary>Gives the resolver a transfer's host names are looked up with.</summary>
    /// <param name="curlResolver">The resolver curl would use.</param>
    /// <returns><paramref name="curlResolver" /> itself without a hook, otherwise the hook's resolver.</returns>
    public IDnsResolver ResolverOver(IDnsResolver curlResolver) =>
        Hook is null ? curlResolver : Hook.ResolverOver(curlResolver);
}
