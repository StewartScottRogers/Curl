namespace Curl.Conformance.Differential.Protocols.Core;

/// <summary>Opens passive data listeners (a loopback <c>TcpListener</c> in the host, a stream in unit tests).</summary>
public interface IPassiveDataListenerOpener
{
    /// <summary>Starts listening for one data connection.</summary>
    IPassiveDataListener Open();
}
