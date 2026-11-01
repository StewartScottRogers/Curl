namespace Curl.Extended.Protocol.Redis.Wire;

/// <summary>
/// Writes a reply as <c>redis-cli --raw</c> does (ADR-1079): each string, number, error and null on its own line
/// (a null an empty line, a boolean <c>true</c> or <c>false</c>), and an aggregate as its elements in order, a map's
/// keys and values alternating.
/// </summary>
internal static class RespReplyRendering
{
    private static readonly byte[] True = "true"u8.ToArray();

    private static readonly byte[] False = "false"u8.ToArray();

    /// <summary>Gives the bytes <paramref name="reply"/> is written to the output as.</summary>
    internal static byte[] Render(RespReply reply)
    {
        using MemoryStream rendered = new();
        Append(rendered, reply);
        return rendered.ToArray();
    }

    private static void Append(MemoryStream rendered, RespReply reply)
    {
        if (reply.IsAggregate)
        {
            foreach (RespReply element in reply.Elements)
            {
                Append(rendered, element);
            }

            return;
        }

        rendered.Write(LineOf(reply));
        rendered.WriteByte((byte)'\n');
    }

    private static byte[] LineOf(RespReply reply) => reply.Kind == RespReplyKind.Boolean
        ? reply.Payload[0] == (byte)'t' ? True : False
        : reply.Payload;
}
