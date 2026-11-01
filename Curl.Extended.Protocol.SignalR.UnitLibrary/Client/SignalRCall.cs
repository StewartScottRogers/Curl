using Curl.Extended.Protocol.SignalR.HubProtocol;
using Curl.Extended.Protocol.SignalR.Json;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.SignalR.Client;

/// <summary>
/// Reads the <c>-d</c> message <c>{"type":1|4,"target":"Method","arguments":[...]}</c> into the Invocation (type 1, the
/// default) or StreamInvocation (type 4) record the client sends, with the invocation id <see cref="InvocationId"/>
/// so the hub answers it (HubProtocol.md, "Invocation" and "StreamInvocation").
/// </summary>
internal static class SignalRCall
{
    /// <summary>The invocation id the one call of a transfer carries.</summary>
    public const string InvocationId = "1";

    private const string Usage = "SignalR: -d must be a JSON object {\"type\":1|4,\"target\":\"Method\",\"arguments\":[...]}";

    /// <summary>Gives the record to send for <paramref name="postData"/>, or <see langword="null"/> without <c>-d</c>.</summary>
    /// <exception cref="SignalRHubException">The data is not such a message (exit 2).</exception>
    public static byte[]? RecordFor(ReadOnlyMemory<byte>? postData)
    {
        if (postData is not { } data)
        {
            return null;
        }

        try
        {
            return Record(JsonFieldReading.ReadObject(data.Span));
        }
        catch (SignalRWireFormatException failure)
        {
            throw new SignalRHubException(CurlExitCode.FailedInit, $"{Usage}: {failure.Message}");
        }
    }

    private static byte[] Record(JsonObjectFields fields)
    {
        string target = fields.RequiredString("target");
        IReadOnlyList<string> arguments = fields.OptionalRawJsonArray("arguments");
        return (fields.OptionalInt32("type") ?? 1) switch
        {
            1 => HubMessageWriter.WriteInvocation(new InvocationMessage(InvocationId, target, arguments, [])),
            4 => HubMessageWriter.WriteStreamInvocation(new StreamInvocationMessage(InvocationId, target, arguments, [])),
            int type => throw new SignalRHubException(CurlExitCode.FailedInit, $"{Usage}: type {type} is neither 1 nor 4"),
        };
    }
}
