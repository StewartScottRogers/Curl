namespace Curl.Extension.Protocol.McpClient.Stdio;

/// <summary>Passes every call to a real server process and counts the calls to <see cref="Kill"/>.</summary>
internal sealed class KillCountingMcpServerProcess(IMcpServerProcess process) : IMcpServerProcess
{
    /// <summary>How many times the session killed the server.</summary>
    public int KillCount { get; private set; }

    /// <inheritdoc/>
    public Stream StandardInput => process.StandardInput;

    /// <inheritdoc/>
    public Stream StandardOutput => process.StandardOutput;

    /// <inheritdoc/>
    public Task WaitForExitAsync(CancellationToken cancellationToken) => process.WaitForExitAsync(cancellationToken);

    /// <inheritdoc/>
    public void Kill()
    {
        KillCount++;
        process.Kill();
    }

    /// <inheritdoc/>
    public void Dispose() => process.Dispose();
}
