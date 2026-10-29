using Curl.Protocol.Abstractions;

namespace Curl.Extension.Composition;

/// <summary>
/// The one place a host learns which protocol handlers Curl's extensions add beyond upstream
/// curl's (ADR-0999). The engine never references an extension: a host passes
/// <see cref="For" />'s factory to the engine's composition, which builds the extensions'
/// handlers over the same transports as curl's own and puts them after curl's, so a scheme
/// both claim fails composition loudly rather than replacing curl's handler.
/// </summary>
public static class ExtensionProtocolHandlers
{
    /// <summary>The environment variable that turns every extension off when set to <c>0</c>.</summary>
    public const string SwitchVariable = "CURL_EXTENSIONS";

    /// <summary>
    /// Gives the factory a host passes to the engine, or <see langword="null" /> when
    /// <see cref="SwitchVariable" /> is <c>0</c>, which leaves exactly upstream curl's handlers.
    /// </summary>
    /// <param name="readEnvironmentVariable">Reads the run's environment.</param>
    /// <returns>The factory, or <see langword="null" /> for no extensions.</returns>
    public static Func<ProtocolHandlerServices, IEnumerable<IProtocolHandler>>? For(Func<string, string?> readEnvironmentVariable)
    {
        ArgumentNullException.ThrowIfNull(readEnvironmentVariable);
        return readEnvironmentVariable(SwitchVariable) == "0" ? null : Create;
    }

    /// <summary>Builds every registered extension's handler over <paramref name="services" />, in registration order.</summary>
    /// <param name="services">The engine's transports.</param>
    /// <returns>One handler per entry of <see cref="ExtensionCatalogue.Entries" />.</returns>
    public static IEnumerable<IProtocolHandler> Create(ProtocolHandlerServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return CreateFrom(ExtensionCatalogue.Entries, services);
    }

    /// <summary>Builds one handler per factory over <paramref name="services" />, in order.</summary>
    /// <param name="factories">The extensions' handler factories.</param>
    /// <param name="services">The engine's transports.</param>
    /// <returns>The handlers.</returns>
    internal static IEnumerable<IProtocolHandler> CreateFrom(
        IEnumerable<Func<ProtocolHandlerServices, IProtocolHandler>> factories,
        ProtocolHandlerServices services) =>
        [.. factories.Select(factory => factory(services))];
}
