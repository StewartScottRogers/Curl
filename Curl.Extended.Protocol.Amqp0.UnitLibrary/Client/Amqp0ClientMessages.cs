using System.Globalization;

namespace Curl.Extended.Protocol.Amqp0.Client;

/// <summary>The error messages an AMQP 0-9-1 transfer ends with (ADR-1060); the ones curl also gives are its text.</summary>
internal static class Amqp0ClientMessages
{
    /// <summary>The text for a URL whose path is not <c>/virtual-host/queue</c>.</summary>
    internal const string NoQueue = "No AMQP queue found: the URL path must be /<virtual host>/<queue>";

    /// <summary>curl's text for the peer closing the connection while a frame is awaited.</summary>
    internal const string ConnectionDisconnected = "Connection disconnected";

    /// <summary>curl's text for a failed receive.</summary>
    internal const string ReceiveFailed = "Failure when receiving data from the peer";

    /// <summary>curl's text for a failed send.</summary>
    internal const string SendFailed = "Failed sending data to the peer";

    /// <summary>The text for a server that offers no PLAIN mechanism, naming the ones it offers.</summary>
    internal static string NoPlainMechanism(string offered) => $"AMQP server offers no PLAIN mechanism (it offers \"{offered}\")";

    /// <summary>The text for a method the flow did not expect at this point, naming its class and method ids.</summary>
    internal static string UnexpectedMethod(ushort classId, ushort methodId) =>
        string.Create(CultureInfo.InvariantCulture, $"Weird server reply: unexpected AMQP method {classId}.{methodId}");

    /// <summary>The text for frames from the server that break the wire format, naming the reply code and the fault.</summary>
    internal static string MalformedFrame(ushort replyCode, string reason) =>
        string.Create(CultureInfo.InvariantCulture, $"Weird server reply: AMQP {replyCode} {reason}");

    /// <summary>The text for a connection.close or channel.close from the server, naming its reply code and text.</summary>
    internal static string ClosedByServer(string methodName, ushort replyCode, string replyText) =>
        string.Create(CultureInfo.InvariantCulture, $"AMQP server sent {methodName}: reply code {replyCode}, {replyText}");

    /// <summary>curl's text for output that could not be written.</summary>
    internal static string OutputWriteFailed(int passed) =>
        string.Create(CultureInfo.InvariantCulture, $"Failure writing output to destination, passed {passed} returned 0");
}
