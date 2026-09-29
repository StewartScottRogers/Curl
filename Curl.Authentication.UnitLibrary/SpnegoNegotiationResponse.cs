using System.Formats.Asn1;

namespace Curl.Authentication;

/// <summary>
/// A NegTokenResp (RFC 4178 section 4.2.2): the acceptor's answer, and every later
/// initiator token, in a SPNEGO exchange. Every field is optional.
/// </summary>
/// <param name="State">The <c>negState</c>, or null when absent.</param>
/// <param name="SupportedMechanism">The <c>supportedMech</c> the acceptor chose, as a dotted object identifier, or null when absent.</param>
/// <param name="ResponseToken">The <c>responseToken</c>, the chosen mechanism's own token, or null when absent.</param>
/// <param name="MechanismListMic">The <c>mechListMIC</c>, or null when absent.</param>
internal sealed record SpnegoNegotiationResponse(
    SpnegoNegotiationState? State,
    string? SupportedMechanism,
    ReadOnlyMemory<byte>? ResponseToken,
    ReadOnlyMemory<byte>? MechanismListMic)
{
    /// <summary>Encodes the NegTokenResp as a NegotiationToken's <c>[1]</c> choice, with no framing.</summary>
    /// <returns>The DER encoding.</returns>
    public byte[] Encode()
    {
        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence(SpnegoAsn1Tags.NegTokenResp))
        using (writer.PushSequence())
        {
            if (State is SpnegoNegotiationState state)
            {
                using (writer.PushSequence(SpnegoAsn1Tags.Field(0)))
                {
                    writer.WriteEnumeratedValue(state);
                }
            }

            if (SupportedMechanism is not null)
            {
                using (writer.PushSequence(SpnegoAsn1Tags.Field(1)))
                {
                    writer.WriteObjectIdentifier(SupportedMechanism);
                }
            }

            WriteOctetStringField(writer, 2, ResponseToken);
            WriteOctetStringField(writer, 3, MechanismListMic);
        }

        return writer.Encode();
    }

    /// <summary>
    /// Decodes a NegTokenResp: a NegotiationToken's <c>[1]</c> choice that fills
    /// <paramref name="bytes" />, as an acceptor sends it, with no framing.
    /// </summary>
    /// <param name="bytes">The token, already base64-decoded.</param>
    /// <returns>The NegTokenResp.</returns>
    /// <exception cref="SpnegoTokenException">
    /// <see cref="SpnegoTokenError.UnexpectedToken" /> for any other token;
    /// <see cref="SpnegoTokenError.Malformed" /> for bytes that are not a NegTokenResp.
    /// </exception>
    public static SpnegoNegotiationResponse Decode(ReadOnlyMemory<byte> bytes)
    {
        try
        {
            AsnReader token = new(bytes, AsnEncodingRules.BER);
            if (!token.PeekTag().HasSameClassAndValue(SpnegoAsn1Tags.NegTokenResp))
            {
                throw new SpnegoTokenException(SpnegoTokenError.UnexpectedToken);
            }

            AsnReader choice = token.ReadSequence(SpnegoAsn1Tags.NegTokenResp);
            token.ThrowIfNotEmpty();
            AsnReader fields = choice.ReadSequence();
            choice.ThrowIfNotEmpty();
            SpnegoNegotiationResponse response = new(
                ReadField(fields, 0, ReadState),
                ReadField(fields, 1, ReadObjectIdentifier),
                ReadField(fields, 2, ReadOctetString),
                ReadField(fields, 3, ReadOctetString));
            fields.ThrowIfNotEmpty();
            return response;
        }
        catch (AsnContentException)
        {
            throw new SpnegoTokenException(SpnegoTokenError.Malformed);
        }
    }

    private static void WriteOctetStringField(AsnWriter writer, int number, ReadOnlyMemory<byte>? value)
    {
        if (value is ReadOnlyMemory<byte> bytes)
        {
            using (writer.PushSequence(SpnegoAsn1Tags.Field(number)))
            {
                writer.WriteOctetString(bytes.Span);
            }
        }
    }

    private static T? ReadField<T>(AsnReader fields, int number, Func<AsnReader, T> readValue)
    {
        Asn1Tag tag = SpnegoAsn1Tags.Field(number);
        if (!fields.HasData || !fields.PeekTag().HasSameClassAndValue(tag))
        {
            return default;
        }

        AsnReader field = fields.ReadSequence(tag);
        T value = readValue(field);
        field.ThrowIfNotEmpty();
        return value;
    }

    private static SpnegoNegotiationState? ReadState(AsnReader field)
    {
        SpnegoNegotiationState state = field.ReadEnumeratedValue<SpnegoNegotiationState>();
        return Enum.IsDefined(state) ? state : throw new AsnContentException();
    }

    private static string? ReadObjectIdentifier(AsnReader field) => field.ReadObjectIdentifier();

    private static ReadOnlyMemory<byte>? ReadOctetString(AsnReader field) => field.ReadOctetString();
}
