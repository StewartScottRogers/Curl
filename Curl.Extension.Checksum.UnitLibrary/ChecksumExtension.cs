using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Description;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.Checksum;

/// <summary>
/// The <c>--checksum &lt;alg&gt;:&lt;hex&gt;</c> extension (ADR-1096): hashes the response body as it is written
/// with a FIPS 180-4 secure hash and fails the transfer with exit 61 when the digest differs from the one given.
/// <c>Curl.Extension.Composition</c> registers its option, description and transfer hook.
/// </summary>
public static class ChecksumExtension
{
    /// <summary>Gets the <c>--checksum &lt;alg&gt;:&lt;hex&gt;</c> option.</summary>
    public static ExtensionOption Option { get; } = new(
        "checksum",
        ExtensionOptionArgument.Value,
        "Fail with exit 61 unless the body's <alg>:<hex> digest matches (sha1, sha256, sha384, sha512)");

    /// <summary>Gets what <c>curl --extensions</c> says about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "checksum",
        "Verifies a download against an expected SHA-1 or SHA-2 digest and fails the transfer when it differs (FIPS 180-4).",
        [],
        [Option],
        new Uri("https://csrc.nist.gov/pubs/fips/180-4/upd1/final"));

    /// <summary>Gives the extension's transfer hook when the option group gave <c>--checksum</c>.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <returns>
    /// The hook verifying the expected digest, a hook that refuses the transfer when the value is not
    /// <c>&lt;alg&gt;:&lt;hex&gt;</c> with a known algorithm and a digest of its length, or <see langword="null" />
    /// when <c>--checksum</c> was not given.
    /// </returns>
    public static ITransferHook? HookFor(ExtensionOptionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.ValueOf(Option) switch
        {
            null => null,
            var text when ExpectedChecksum.TryParse(text) is { } expected => new ChecksumTransferHook(expected),
            _ => new InvalidChecksumTransferHook(),
        };
    }
}
