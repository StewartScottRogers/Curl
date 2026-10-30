using Curl.Extension.SignalR.Json;

namespace Curl.Extension.SignalR.Negotiate;

/// <summary>Reads the JSON body of a negotiate response, version 0 or 1 (TransportProtocols.md).</summary>
internal static class NegotiateResponseReader
{
    /// <summary>Reads the body into a <see cref="NegotiateResponse"/>.</summary>
    /// <exception cref="SignalRWireFormatException">The body is not a negotiate response.</exception>
    public static NegotiateResponse Read(ReadOnlySpan<byte> body)
    {
        JsonObjectFields fields = JsonFieldReading.ReadObject(body);
        return new NegotiateResponse(
            fields.OptionalInt32("negotiateVersion") ?? 0,
            fields.OptionalString("connectionId"),
            fields.OptionalString("connectionToken"),
            fields.OptionalObjectArray("availableTransports").Select(ReadTransport).ToArray(),
            fields.OptionalString("url"),
            fields.OptionalString("accessToken"),
            fields.OptionalString("error"));
    }

    private static NegotiateTransport ReadTransport(JsonObjectFields transport) =>
        new(transport.RequiredString("transport"), transport.OptionalStringArray("transferFormats"));
}
