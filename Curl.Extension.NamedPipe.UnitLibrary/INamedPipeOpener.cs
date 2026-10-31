namespace Curl.Extension.NamedPipe;

/// <summary>Opens the client end of a Windows named pipe as a byte stream.</summary>
public interface INamedPipeOpener
{
    /// <summary>Connects to the pipe <paramref name="name" /> names.</summary>
    /// <param name="name">The pipe.</param>
    /// <param name="cancellationToken">Cancels the connect.</param>
    /// <returns>The connected pipe, read and written in byte mode.</returns>
    /// <exception cref="IOException">The pipe is not there or has no free instance.</exception>
    /// <exception cref="TimeoutException">The pipe did not accept the connect.</exception>
    /// <exception cref="UnauthorizedAccessException">The pipe refused this user.</exception>
    ValueTask<Stream> OpenAsync(NamedPipeName name, CancellationToken cancellationToken);
}
