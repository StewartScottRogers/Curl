namespace Curl.Extension.Protocol.Plugin;

/// <summary>A running helper program; disposing it ends the program if it is still running.</summary>
public interface IHelperProcess : IAsyncDisposable
{
    /// <summary>Gets the helper's standard input, where Curl writes its commands.</summary>
    Stream Input { get; }

    /// <summary>Gets the helper's standard output, where it answers.</summary>
    Stream Output { get; }

    /// <summary>Waits for the helper to end.</summary>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>The helper's exit status.</returns>
    Task<int> WaitForExitAsync(CancellationToken cancellationToken);
}
