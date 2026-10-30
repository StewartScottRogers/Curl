using System.Text.Json;
using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Certificates;

namespace Curl.Extension.CertificateTransparency;

/// <summary>
/// Refuses a chain curl accepted unless the server's certificate carries a valid SCT from a log in the
/// <c>--require-sct</c> log list (ADR-1129). The list is read for each new TLS connection.
/// </summary>
/// <param name="logListPath">The log list file <c>--require-sct</c> named.</param>
/// <param name="readFile">Reads a whole file.</param>
/// <param name="timeProvider">Gives the time no SCT may be later than.</param>
internal sealed class CertificateTransparencyCheckHook(string logListPath, Func<string, CancellationToken, Task<byte[]>> readFile, TimeProvider timeProvider) : ICertificateCheckHook
{
    /// <inheritdoc />
    public ExtensionOption Option => CertificateTransparencyExtension.Option;

    /// <inheritdoc />
    public async ValueTask<string?> RefusalOfAsync(IReadOnlyList<ReadOnlyMemory<byte>> peerCertificates, string host, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, byte[]> keysByLogId;
        try
        {
            keysByLogId = CertificateTransparencyLogList.KeysByLogId(await readFile(logListPath, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or FormatException)
        {
            return $"Certificate Transparency: the log list {logListPath} could not be read: {exception.Message}";
        }

        return SignedCertificateTimestampVerifier.RefusalOf(peerCertificates, keysByLogId, timeProvider.GetUtcNow());
    }
}
