namespace Curl.Protocol.Abstractions;

/// <summary>A program an <see cref="IExtensionProcessStarter" /> started; disposing it releases it without ending it.</summary>
public interface IExtensionProcess : IDisposable
{
    /// <summary>Gets the program's standard input, where Curl writes.</summary>
    Stream StandardInput { get; }

    /// <summary>Gets the program's standard output, where Curl reads.</summary>
    Stream StandardOutput { get; }

    /// <summary>Waits for the program to end.</summary>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>The program's exit status.</returns>
    Task<int> WaitForExitAsync(CancellationToken cancellationToken);

    /// <summary>Ends the program and the programs it started, if it is still running.</summary>
    void Kill();
}
