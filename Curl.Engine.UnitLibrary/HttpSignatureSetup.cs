using Curl.Cli;
using Curl.HttpMessageSignatures;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// The outcome of curl 8.22.0's setup checks for the <c>--httpsig-*</c> options
/// (<c>src/config2setopts.c</c>, <c>httpsig_setopts</c>): the settings to sign with, none when
/// no such option is given, or the refusal curl prints with <c>errorf</c> and the exit code it
/// ends with (BL-2182).
/// </summary>
/// <param name="Settings">The settings, or <see langword="null" /> when none is given or the checks refuse.</param>
/// <param name="ExitCode">The exit code of a refusal; <see cref="CurlExitCode.Ok" /> otherwise.</param>
/// <param name="ErrorLine">The line a refusal prints, such as <c>curl: --httpsig-key is required</c>; <see langword="null" /> otherwise.</param>
internal sealed record HttpSignatureSetup(HttpSignatureSettings? Settings, CurlExitCode ExitCode, string? ErrorLine)
{
    /// <summary>The setup of a command line that gives none of the options.</summary>
    internal static readonly HttpSignatureSetup None = new(null, CurlExitCode.Ok, null);

    /// <summary>
    /// Checks <paramref name="options" /> as curl does, in its order: a missing key, a missing key
    /// id, then an algorithm other than <c>ed25519</c> (the default) or <c>hmac-sha256</c> in any
    /// case, each exit 2; then reads a key given as <c>@file</c>, exit 26 when it cannot be read
    /// and 43 when it is empty.
    /// </summary>
    /// <param name="options">The parsed command line.</param>
    /// <returns>The outcome.</returns>
    internal static HttpSignatureSetup Of(CommandLineOptions options)
    {
        if (!IsRequested(options))
        {
            return None;
        }

        if (options.HttpSignatureKey is not { } key)
        {
            return Refused(CurlExitCode.FailedInit, "curl: --httpsig-key is required");
        }

        if (options.HttpSignatureKeyId is not { } keyId)
        {
            return Refused(CurlExitCode.FailedInit, "curl: --httpsig-keyid is required");
        }

        return AlgorithmOf(options.HttpSignatureAlgorithm) is { } algorithm
            ? WithKey(algorithm, key, keyId, options.HttpSignatureHeaders)
            : Refused(CurlExitCode.FailedInit, $"curl: --httpsig-algo: unsupported algorithm '{options.HttpSignatureAlgorithm}'");
    }

    private static bool IsRequested(CommandLineOptions options) =>
        options.HttpSignatureAlgorithm is not null
        || options.HttpSignatureKey is not null
        || options.HttpSignatureKeyId is not null
        || options.HttpSignatureHeaders is not null;

    /// <summary>
    /// Gives the algorithm's name in lower case, <c>ed25519</c> when none is given, or
    /// <see langword="null" /> for one curl does not support.
    /// </summary>
    private static string? AlgorithmOf(string? given) =>
        given is null || given.Equals(HttpSignatureAlgorithmNames.Ed25519, StringComparison.OrdinalIgnoreCase)
            ? HttpSignatureAlgorithmNames.Ed25519
            : given.Equals(HttpSignatureAlgorithmNames.HmacSha256, StringComparison.OrdinalIgnoreCase) ? HttpSignatureAlgorithmNames.HmacSha256 : null;

    /// <summary>Gives the settings with the key as given, or as the text of the file <c>@file</c> names.</summary>
    private static HttpSignatureSetup WithKey(string algorithm, string key, string keyId, string? components)
    {
        if (!key.StartsWith('@'))
        {
            return new(new HttpSignatureSettings(algorithm, key, keyId, components), CurlExitCode.Ok, null);
        }

        string fileName = key[1..];
        string? text = ReadKeyFile(fileName);
        if (text is null)
        {
            return Refused(CurlExitCode.ReadError, $"curl: httpsig: cannot open key file '{fileName}'");
        }

        return text.Length == 0
            ? Refused(CurlExitCode.BadFunctionArgument, $"curl: httpsig: key file '{fileName}' is empty")
            : new(new HttpSignatureSettings(algorithm, text, keyId, components), CurlExitCode.Ok, null);
    }

    /// <summary>Reads the key file's text, or gives <see langword="null" /> when it cannot be read.</summary>
    private static string? ReadKeyFile(string fileName)
    {
        try
        {
            return File.ReadAllText(fileName);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static HttpSignatureSetup Refused(CurlExitCode exitCode, string line) => new(null, exitCode, line);
}
