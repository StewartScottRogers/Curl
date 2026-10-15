using Curl.Protocol.Abstractions;

namespace Curl.Api.Codes;

/// <summary>
/// Converts between a libcurl function's <see cref="CurlCode"/> and the engine's
/// <see cref="CurlExitCode"/>, keeping the number (ADR-0506).
/// </summary>
public static class CurlCodeConversion
{
    /// <summary>Returns the <see cref="CurlExitCode"/> with the same number as <paramref name="code"/>.</summary>
    /// <param name="code">The libcurl result to convert.</param>
    /// <returns>
    /// The exit code with the same number; for an unused <c>CURLE_OBSOLETE*</c> number, that number
    /// as an unnamed <see cref="CurlExitCode"/> value.
    /// </returns>
    public static CurlExitCode ToCurlExitCode(this CurlCode code) => (CurlExitCode)(int)code;

    /// <summary>Returns the <see cref="CurlCode"/> with the same number as <paramref name="exitCode"/>.</summary>
    /// <param name="exitCode">The engine outcome to convert.</param>
    /// <returns>The libcurl result with the same number.</returns>
    public static CurlCode ToCurlCode(this CurlExitCode exitCode) => (CurlCode)(int)exitCode;
}
