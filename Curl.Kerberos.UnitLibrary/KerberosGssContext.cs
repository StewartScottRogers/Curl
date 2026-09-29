using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Curl.Kerberos;

/// <summary>
/// The initiator's side of the GSS-API Kerberos V5 mechanism (RFC 4121, ADR-0171): the
/// initial context token (an AP-REQ whose authenticator carries RFC 4121's checksum, a fresh
/// subkey and a sequence number), the check of the acceptor's AP-REP, and then Wrap and MIC
/// tokens in RFC 4121 section 4.2's format, or RFC 4757 section 7's for an <c>rc4-hmac</c>
/// key. It is what the hand-built route of ADR-0142's security-context seam runs.
/// <see cref="Dispose" /> zeroes its keys.
/// </summary>
public sealed class KerberosGssContext : IDisposable
{
    /// <summary>RFC 4121 section 4.1.1's checksum type, which carries the context flags rather than a checksum.</summary>
    public const int GssChecksumType = 0x8003;

    private const int ApRequestAuthenticatorUsage = 11;
    private const int ApReplyUsage = 12;
    private const int CredentialUsage = 14;
    private const int ChannelBindingsSize = 16;
    private const uint SequenceNumberMask = 0x3FFFFFFF;

    private readonly KerberosCredential serviceTicket;
    private readonly KerberosGssContextOptions options;
    private readonly TimeProvider timeProvider;
    private readonly IKerberosRandomSource randomSource;
    private readonly KerberosEncryption encryption;
    private KerberosKey? subkey;
    private uint sendSequence;
    private DateTimeOffset authenticatorTime;
    private int authenticatorMicroseconds;
    private KerberosGssMessageProtection? protection;

    /// <summary>Initializes a new instance of the <see cref="KerberosGssContext" /> class.</summary>
    /// <param name="serviceTicket">The ticket for the acceptor, e.g. from <see cref="KerberosKdcClient" />; it stays the caller's.</param>
    /// <param name="options">The flags and delegation asked for.</param>
    /// <param name="timeProvider">Gives the authenticator's time.</param>
    /// <param name="randomSource">Gives the subkey, the sequence number and confounders.</param>
    /// <exception cref="KerberosCryptographyException">The ticket's session key is of an encryption type this library does not have.</exception>
    public KerberosGssContext(KerberosCredential serviceTicket, KerberosGssContextOptions options, TimeProvider timeProvider, IKerberosRandomSource randomSource)
    {
        ArgumentNullException.ThrowIfNull(serviceTicket);
        ArgumentNullException.ThrowIfNull(options);
        this.serviceTicket = serviceTicket;
        this.options = options;
        this.timeProvider = timeProvider;
        this.randomSource = randomSource;
        encryption = KerberosEncryption.Create((KerberosEncryptionType)serviceTicket.SessionKey.EncryptionType, randomSource);
    }

    /// <summary>
    /// Gets the flags the initial token's checksum carries: those asked for, with
    /// <see cref="KerberosGssFlags.Confidentiality" />, <see cref="KerberosGssFlags.Integrity" /> and
    /// <see cref="KerberosGssFlags.Transfer" /> always added as MIT adds them, and <see cref="KerberosGssFlags.Delegation" /> when a
    /// ticket-granting ticket was forwarded. <see cref="KerberosGssFlags.None" /> until then.
    /// </summary>
    public KerberosGssFlags Flags { get; private set; }

    /// <summary>Gets whether the context is established, so Wrap and MIC tokens can be made.</summary>
    public bool IsCompleted => protection is not null;

    /// <summary>
    /// Gives the next token for the acceptor. The first call, whose input is ignored, gives
    /// the initial context token; without mutual authentication the context is then
    /// established. With it, the second call takes the acceptor's AP-REP token, checks it, and
    /// gives an empty token, and the context is established.
    /// </summary>
    /// <param name="incomingToken">The acceptor's token; empty on the first call.</param>
    /// <returns>The token to send; empty when there is nothing to send.</returns>
    /// <exception cref="InvalidOperationException">The context is already established.</exception>
    /// <exception cref="KerberosGssException">The acceptor's token is malformed, a KRB-ERROR, or fails mutual authentication.</exception>
    public byte[] NextToken(ReadOnlySpan<byte> incomingToken)
    {
        if (IsCompleted)
        {
            throw new InvalidOperationException("The GSS-API Kerberos context is already established.");
        }

        if (subkey is null)
        {
            return InitialToken();
        }

        ReadApReply(incomingToken);
        return [];
    }

    /// <summary>Makes a Wrap token for <paramref name="message" />.</summary>
    /// <param name="message">The message.</param>
    /// <param name="encrypt">Whether to encrypt it, or only protect its integrity.</param>
    /// <returns>The token.</returns>
    /// <exception cref="InvalidOperationException">The context is not established.</exception>
    public byte[] Wrap(ReadOnlySpan<byte> message, bool encrypt) => Protection.Wrap(message, encrypt);

    /// <summary>Checks the acceptor's Wrap token and gives its message.</summary>
    /// <param name="token">The token.</param>
    /// <returns>The message, and whether it was encrypted.</returns>
    /// <exception cref="InvalidOperationException">The context is not established.</exception>
    /// <exception cref="KerberosGssException">The token is malformed, altered or out of sequence.</exception>
    public KerberosGssUnwrapped Unwrap(ReadOnlySpan<byte> token) => Protection.Unwrap(token);

    /// <summary>Makes a MIC token for <paramref name="message" />.</summary>
    /// <param name="message">The message.</param>
    /// <returns>The token.</returns>
    /// <exception cref="InvalidOperationException">The context is not established.</exception>
    public byte[] GetMic(ReadOnlySpan<byte> message) => Protection.GetMic(message);

    /// <summary>Checks the acceptor's MIC token for <paramref name="message" />.</summary>
    /// <param name="message">The message.</param>
    /// <param name="token">The token.</param>
    /// <exception cref="InvalidOperationException">The context is not established.</exception>
    /// <exception cref="KerberosGssException">The token is malformed, does not match or is out of sequence.</exception>
    public void VerifyMic(ReadOnlySpan<byte> message, ReadOnlySpan<byte> token) => Protection.VerifyMic(message, token);

    /// <summary>Zeroes the subkey and the context key.</summary>
    public void Dispose()
    {
        subkey?.Dispose();
        protection?.Dispose();
    }

    private KerberosGssMessageProtection Protection =>
        protection ?? throw new InvalidOperationException("The GSS-API Kerberos context is not established.");

    private static KerberosPrincipalName NameOf(KerberosPrincipal principal) => new(principal.NameType, principal.Components);

    private bool Delegates() =>
        options.ForwardedTicketGrantingTicket is not null
        && (options.Delegation == KerberosDelegation.Always
            || (options.Delegation == KerberosDelegation.Policy && serviceTicket.Flags.HasFlag(KerberosTicketFlags.OkAsDelegate)));

    private byte[] InitialToken()
    {
        byte[] subkeyBytes = new byte[encryption.KeySize];
        randomSource.Fill(subkeyBytes);
        subkey = new KerberosKey(serviceTicket.SessionKey.EncryptionType, subkeyBytes);
        Span<byte> sequenceBytes = stackalloc byte[sizeof(uint)];
        randomSource.Fill(sequenceBytes);
        sendSequence = BinaryPrimitives.ReadUInt32BigEndian(sequenceBytes) & SequenceNumberMask;
        (authenticatorTime, authenticatorMicroseconds) = KerberosClock.Now(timeProvider);
        bool delegates = Delegates();
        Flags = (options.RequestedFlags & ~KerberosGssFlags.Delegation) | KerberosGssFlags.Confidentiality | KerberosGssFlags.Integrity
            | KerberosGssFlags.Transfer | (delegates ? KerberosGssFlags.Delegation : KerberosGssFlags.None);

        KerberosAuthenticator authenticator = new()
        {
            ClientRealm = serviceTicket.Client.Realm,
            ClientName = NameOf(serviceTicket.Client),
            Checksum = new KerberosChecksum(GssChecksumType, ChecksumValue(delegates)),
            ClientMicroseconds = authenticatorMicroseconds,
            ClientTime = authenticatorTime,
            Subkey = subkey,
            SequenceNumber = sendSequence,
        };
        byte[] plaintext = authenticator.Encode();
        byte[] cipher = encryption.Encrypt(serviceTicket.SessionKey.Value, ApRequestAuthenticatorUsage, plaintext);
        CryptographicOperations.ZeroMemory(plaintext);
        bool mutual = Flags.HasFlag(KerberosGssFlags.MutualAuthentication);
        KerberosApRequest request = new(
            mutual ? KerberosApOptions.MutualRequired : KerberosApOptions.None,
            serviceTicket.Ticket,
            new KerberosEncryptedData(serviceTicket.SessionKey.EncryptionType, null, cipher));
        if (!mutual)
        {
            Establish(subkey, acceptorSubkey: false, sendSequence);
        }

        return KerberosGssToken.Frame(KerberosGssToken.ApRequestTokenId, request.Encode());
    }

    /// <summary>
    /// RFC 4121 section 4.1.1: the length of the channel bindings (16), their MD5 (zeros when
    /// none are given), the flags, all little-endian, then with delegation the option 1, the
    /// KRB-CRED's length and the KRB-CRED.
    /// </summary>
    private byte[] ChecksumValue(bool delegates)
    {
        byte[] credential = delegates ? ForwardedCredential(options.ForwardedTicketGrantingTicket!) : [];
        int delegationSize = delegates ? (2 * sizeof(ushort)) + credential.Length : 0;
        byte[] value = new byte[sizeof(uint) + ChannelBindingsSize + sizeof(uint) + delegationSize];
        BinaryPrimitives.WriteUInt32LittleEndian(value, ChannelBindingsSize);
        if (options.ChannelBindings is { } applicationData)
        {
            ChannelBindingsHash(applicationData).CopyTo(value, sizeof(uint));
        }

        BinaryPrimitives.WriteUInt32LittleEndian(value.AsSpan(sizeof(uint) + ChannelBindingsSize), (uint)Flags);
        if (delegates)
        {
            Span<byte> delegation = value.AsSpan(sizeof(uint) + ChannelBindingsSize + sizeof(uint));
            BinaryPrimitives.WriteUInt16LittleEndian(delegation, 1);
            BinaryPrimitives.WriteUInt16LittleEndian(delegation[sizeof(ushort)..], (ushort)credential.Length);
            credential.CopyTo(delegation[(2 * sizeof(ushort))..]);
        }

        return value;
    }

    /// <summary>
    /// The MD5 of RFC 2744's <c>gss_channel_bindings_struct</c> as MIT's
    /// <c>kg_checksum_channel_bindings</c> lays it out: the initiator's address type and
    /// address, the acceptor's, then the application data, each length and type a
    /// little-endian 32-bit integer; curl passes no addresses, so both are type 0 and empty.
    /// </summary>
    private static byte[] ChannelBindingsHash(byte[] applicationData)
    {
        const int AddressFieldsSize = 4 * sizeof(uint);
        byte[] structure = new byte[AddressFieldsSize + sizeof(uint) + applicationData.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(structure.AsSpan(AddressFieldsSize), (uint)applicationData.Length);
        applicationData.CopyTo(structure, AddressFieldsSize + sizeof(uint));
        return MD5.HashData(structure);
    }

    /// <summary>The forwarded ticket-granting ticket as a KRB-CRED, its part encrypted in the service ticket's session key (not the subkey), as MIT sends it.</summary>
    private byte[] ForwardedCredential(KerberosCredential ticketGrantingTicket)
    {
        using KerberosEncryptedCredentialPart part = new()
        {
            Credentials =
            [
                new KerberosCredentialInfo
                {
                    Key = new KerberosKey(ticketGrantingTicket.SessionKey.EncryptionType, ticketGrantingTicket.SessionKey.Value.ToArray()),
                    ClientRealm = ticketGrantingTicket.Client.Realm,
                    ClientName = NameOf(ticketGrantingTicket.Client),
                    Flags = ticketGrantingTicket.Flags,
                    AuthenticationTime = ticketGrantingTicket.AuthenticationTime,
                    EndTime = ticketGrantingTicket.EndTime,
                    ServerRealm = ticketGrantingTicket.Server.Realm,
                    ServerName = NameOf(ticketGrantingTicket.Server),
                },
            ],
            Timestamp = authenticatorTime,
            Microseconds = authenticatorMicroseconds,
        };
        byte[] plaintext = part.Encode();
        byte[] cipher = encryption.Encrypt(serviceTicket.SessionKey.Value, CredentialUsage, plaintext);
        CryptographicOperations.ZeroMemory(plaintext);
        return new KerberosCredentialMessage(
            [ticketGrantingTicket.Ticket],
            new KerberosEncryptedData(serviceTicket.SessionKey.EncryptionType, null, cipher)).Encode();
    }

    /// <summary>Reads the acceptor's answer: a KRB-ERROR is refused; an AP-REP must decrypt in the session key and echo the authenticator's time.</summary>
    private void ReadApReply(ReadOnlySpan<byte> token)
    {
        ReadOnlySpan<byte> inner = KerberosGssToken.Unframe(token);
        ushort tokenId = KerberosGssToken.TokenIdOf(inner);
        byte[] body = inner[sizeof(ushort)..].ToArray();
        if (tokenId == KerberosGssToken.ErrorTokenId)
        {
            throw new KerberosGssException(KerberosGssError.AcceptorError, Decode(() => KerberosErrorMessage.Decode(body)).ErrorCode);
        }

        KerberosApReply reply = tokenId == KerberosGssToken.ApReplyTokenId ? Decode(() => KerberosApReply.Decode(body)) : throw KerberosGssToken.Malformed();
        byte[] plaintext;
        try
        {
            plaintext = KerberosAsn1.WithoutPadding(encryption.Decrypt(serviceTicket.SessionKey.Value, ApReplyUsage, reply.EncryptedPart.Cipher));
        }
        catch (KerberosCryptographyException)
        {
            throw new KerberosGssException(KerberosGssError.MutualAuthenticationFailed);
        }

        using KerberosEncryptedApReplyPart part = DecodeAndZero(plaintext);
        if (part.ClientTime != authenticatorTime || part.ClientMicroseconds != authenticatorMicroseconds)
        {
            throw new KerberosGssException(KerberosGssError.MutualAuthenticationFailed);
        }

        Establish(part.Subkey ?? subkey!, part.Subkey is not null, part.SequenceNumber ?? 0);
    }

    private static T Decode<T>(Func<T> decode)
    {
        try
        {
            return decode();
        }
        catch (KerberosMessageException)
        {
            throw KerberosGssToken.Malformed();
        }
    }

    private static KerberosEncryptedApReplyPart DecodeAndZero(byte[] plaintext)
    {
        try
        {
            return Decode(() => KerberosEncryptedApReplyPart.Decode(plaintext));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private void Establish(KerberosKey contextKey, bool acceptorSubkey, ulong receiveSequence) =>
        protection = KerberosGssMessageProtection.Create(contextKey, acceptorSubkey, sendSequence, receiveSequence, randomSource);
}
