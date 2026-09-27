namespace Curl.Conformance;

/// <summary>
/// Whether the harness has given up on one sws emulation, shared by the server and every
/// connection it opened: once abandoned, connecting, reading and writing throw, so a curl run
/// past the case's time limit stops at its next exchange instead of running on unseen.
/// </summary>
internal sealed class SwsServerAbandonment
{
    private volatile bool abandoned;

    /// <summary>Gives up on the server; every later exchange with it throws.</summary>
    public void Abandon() => abandoned = true;

    /// <summary>Throws when the server has been abandoned.</summary>
    /// <exception cref="IOException">The server has been abandoned.</exception>
    public void ThrowIfAbandoned()
    {
        if (abandoned)
        {
            throw new IOException("The conformance harness abandoned the sws emulation after the case's time limit.");
        }
    }
}
