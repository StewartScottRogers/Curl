using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// What <c>--engine</c> does in a curl that has no crypto engines, as ADR-0151 decides: Curl has
/// no OpenSSL to load an engine into, so it lists none, ignores a named engine on Windows as the
/// Schannel build does, and elsewhere fails the transfer before it connects as the OpenSSL build
/// fails for an engine it cannot load.
/// </summary>
/// <remarks>
/// Measured 2026-09-28 (ADR-0151): <c>curl --engine list</c> prints
/// <c>Build-time engines:</c> and <c>  &lt;none&gt;</c> on the Schannel build; the OpenSSL build
/// (Ubuntu curl 8.18.0, OpenSSL 3.5.5) fails <c>--engine dynamic</c> with exit 66 and any other
/// name with exit 53, with the texts below.
/// </remarks>
internal static class NoCryptoEngines
{
    /// <summary>The OpenSSL build's error for <c>--engine dynamic</c>, exit 66.</summary>
    internal const string DynamicEngineFailedMessage = "Failed to initialise SSL Engine 'dynamic': error:00000000:lib(0)::reason(0)";

    /// <summary>The OpenSSL build's error for any engine name but <c>dynamic</c>, exit 53.</summary>
    internal const string EngineNotFoundMessage = "Failed to initialize provider: error:12800067:DSO support routines::could not load the shared library";

    /// <summary>The lines <c>--engine list</c> prints on standard output: no build-time engines.</summary>
    internal static IReadOnlyList<string> ListLines { get; } = ["Build-time engines:", "  <none>"];

    /// <summary>
    /// The failure a transfer made with <c>--engine &lt;name&gt;</c> ends with before it connects.
    /// </summary>
    /// <param name="engine">The <c>--engine</c> name, or <see langword="null" /> when not given.</param>
    /// <param name="runsOnWindows">Whether this is the Windows build, which ignores the engine.</param>
    /// <returns>
    /// <see langword="null" /> without an engine or on Windows; otherwise
    /// <see cref="CurlExitCode.SslEngineInitFailed" /> with <see cref="DynamicEngineFailedMessage" />
    /// for <c>dynamic</c>, and <see cref="CurlExitCode.SslEngineNotFound" /> with
    /// <see cref="EngineNotFoundMessage" /> for any other name.
    /// </returns>
    internal static TransferResult? LoadFailure(string? engine, bool runsOnWindows) => (engine, runsOnWindows) switch
    {
        (null, _) or (_, true) => null,
        ("dynamic", _) => TransferResult.Failure(CurlExitCode.SslEngineInitFailed, DynamicEngineFailedMessage),
        _ => TransferResult.Failure(CurlExitCode.SslEngineNotFound, EngineNotFoundMessage),
    };
}
