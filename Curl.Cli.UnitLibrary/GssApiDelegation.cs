namespace Curl.Cli;

/// <summary>
/// Whether GSS-API authentication may hand the user's credentials on to the server, as the last
/// <c>--delegation</c> on the command line chose it.
/// </summary>
public enum GssApiDelegation
{
    /// <summary><c>--delegation none</c>, an unrecognised value, or no <c>--delegation</c>: never delegate.</summary>
    None = 0,

    /// <summary><c>--delegation policy</c>: delegate only when the service ticket is marked ok-as-delegate.</summary>
    Policy,

    /// <summary><c>--delegation always</c>: always delegate.</summary>
    Always,
}
