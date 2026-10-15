using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// Appends a connection's secrets to the file <c>SSLKEYLOGFILE</c> names, one NSS key-log line per
/// secret (<c>CLIENT_RANDOM &lt;client random&gt; &lt;master secret&gt;</c>, lowercase hex), as curl's
/// OpenSSL build does through OpenSSL's key-log callback (BL-2327). A file that cannot be opened or
/// written is skipped silently, as curl skips one it cannot <c>fopen</c>.
/// </summary>
/// <param name="path">The file to append to.</param>
public sealed class NssKeyLogFile(string path) : ITlsKeyLog
{
    private readonly Lock gate = new();

    /// <summary>Gets the file the lines are appended to.</summary>
    public string Path { get; } = path;

    /// <inheritdoc />
    public void Append(string label, ReadOnlySpan<byte> clientRandom, ReadOnlySpan<byte> secret)
    {
        string line = $"{label} {Convert.ToHexStringLower(clientRandom)} {Convert.ToHexStringLower(secret)}\n";
        lock (gate)
        {
            try
            {
                File.AppendAllText(Path, line);
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                // curl writes nothing when the key-log file cannot be opened.
            }
        }
    }
}
