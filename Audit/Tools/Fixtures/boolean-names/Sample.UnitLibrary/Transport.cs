namespace Sample;

// Fixture for Find-BooleanNameMismatches.ps1 -SelfTest: state names renamed to their opposite
// whose doc never uses either word, so only the body and the callers show it. Two the body and
// caller checks must flag and two they must leave alone. Nothing here is real.
public static class Transport
{
    /// <summary>The scheme of the transport over TLS.</summary>
    public const string TrustedScheme = "sample+tls";

    /// <summary>The mode that sends bytes in the clear.</summary>
    public const string UnencryptedMode = "clear";

    /// <summary>Gives the default port of a scheme.</summary>
    /// <param name="scheme">The scheme.</param>
    /// <returns>The port.</returns>
    public static int PortOf(string scheme) => IsUntrusted(scheme) ? TrustedPort : PlainPort;

    /// <summary>The port of the TLS scheme.</summary>
    public const int TrustedPort = 6000;

    /// <summary>The port of the plain scheme.</summary>
    public const int PlainPort = 5000;

    /// <summary>Tells whether a scheme is the TLS one.</summary>
    /// <param name="scheme">The scheme.</param>
    /// <returns><see langword="true" /> for sample+tls.</returns>
    public static bool IsUntrusted(string scheme) => string.Equals(scheme, TrustedScheme, System.StringComparison.Ordinal);

    /// <summary>Tells the transport mode apart.</summary>
    /// <param name="mode">The mode.</param>
    /// <returns><see langword="true" /> for the clear mode.</returns>
    public static bool IsEncrypted(string mode) => mode == UnencryptedMode;

    /// <summary>Tells the transport mode apart.</summary>
    /// <param name="mode">The mode.</param>
    /// <returns><see langword="true" /> for any mode but TLS.</returns>
    public static bool IsUnsafe(string mode) => !string.Equals(mode, TrustedScheme, System.StringComparison.Ordinal);

    /// <summary>Tells the transport mode apart.</summary>
    /// <param name="mode">The mode.</param>
    /// <returns><see langword="true" /> for the clear mode.</returns>
    public static bool IsUnencrypted(string mode) => mode == UnencryptedMode;
}
