using System.Text;

namespace Curl.Tls;

/// <summary>
/// The <c>server_name</c> extension (RFC 6066 section 3): in a ClientHello, a list holding
/// the one DNS host name the client wants; from the server, empty data acknowledging it.
/// </summary>
public static class ServerNameExtension
{
    private const byte HostNameType = 0;

    /// <summary>Returns a ClientHello's <c>server_name</c> naming <paramref name="hostName" />.</summary>
    /// <param name="hostName">The ASCII host name (an IDN already in its A-label form).</param>
    /// <returns>The extension.</returns>
    public static TlsExtension EncodeHostName(string hostName)
    {
        ArgumentNullException.ThrowIfNull(hostName);
        TlsWriter writer = new();
        writer.WriteVector(2, list =>
        {
            list.WriteUInt8(HostNameType);
            list.WriteOpaque(2, Encoding.Latin1.GetBytes(hostName));
        });
        return new TlsExtension(TlsExtensionType.ServerName, writer.ToArray());
    }

    /// <summary>Decodes a ClientHello's <c>server_name</c> data holding one host name.</summary>
    /// <param name="data">The extension data.</param>
    /// <returns>The host name, or the alert the bytes call for (<see cref="TlsAlertDescription.IllegalParameter" /> for a name type other than <c>host_name</c>, <see cref="TlsAlertDescription.DecodeError" /> for an empty name).</returns>
    public static TlsDecodeResult<string> DecodeHostName(byte[] data)
    {
        TlsReader reader = new(data);
        TlsReader list = reader.ReadVector(2);
        byte nameType = list.ReadUInt8();
        byte[] name = list.ReadNonEmptyOpaque(2);
        list.ExpectEnd();
        if (nameType != HostNameType)
        {
            reader.Fail(TlsAlertDescription.IllegalParameter);
        }

        return reader.Finish(Encoding.Latin1.GetString(name));
    }

    /// <summary>Returns the server's empty <c>server_name</c> acknowledging the client's.</summary>
    /// <returns>The extension.</returns>
    public static TlsExtension EncodeAcknowledgement() => new(TlsExtensionType.ServerName, []);

    /// <summary>Checks a server's <c>server_name</c> data, which must be empty.</summary>
    /// <param name="data">The extension data.</param>
    /// <returns><see langword="null" /> when the data is empty; otherwise <see cref="TlsAlertDescription.DecodeError" />.</returns>
    public static TlsAlertDescription? DecodeAcknowledgement(byte[] data) => new TlsReader(data).Finish(true).Alert;
}
