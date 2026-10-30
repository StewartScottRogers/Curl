using System.Security.Cryptography;
using System.Text;
using Curl.Cryptography;
using Curl.Extension.ObliviousHttp.BinaryHttp;
using Curl.Extension.ObliviousHttp.Encapsulation;
using Curl.Extension.ObliviousHttp.Hpke;

namespace Curl.Extension.ObliviousHttp.Relay;

/// <summary>
/// Stands in for curl's handler talking to a gateway's key configuration resource (RFC 9540 "Key Configuration
/// Fetching") and to a relay that forwards to the gateway (RFC 9458 sections 4.3, 4.4 and 5), the gateway written
/// from the RFC with the RFC 9458 Appendix A key. Every request it is given is kept in <see cref="Sent" />.
/// </summary>
internal sealed class FakeObliviousServers
{
    internal const string RelayHost = "relay.example";

    private readonly byte[] gatewayKey = Convert.FromHexString(Rfc9458Example.GatewayPrivateKey);

    public List<ITransferContext> Sent { get; } = [];

    /// <summary>Gets the binary request the gateway opened, or <see langword="null" /> before the relay was reached.</summary>
    public byte[]? OpenedRequest { get; private set; }

    /// <summary>Gets or sets the binary response the gateway encapsulates.</summary>
    public byte[] BinaryResponse { get; set; } = KnownLengthResponse(200, [new("content-type", "text/plain")], "hello");

    /// <summary>Gets or sets the relay's status code.</summary>
    public int RelayStatus { get; set; } = 200;

    /// <summary>Gets or sets the relay's Content-Type.</summary>
    public string? RelayContentType { get; set; } = "message/ohttp-res";

    /// <summary>Gets or sets whether the gateway flips a bit of the sealed response.</summary>
    public bool TamperWithResponse { get; set; }

    /// <summary>Gets the <c>application/ohttp-keys</c> list holding the RFC 9458 Appendix A configuration.</summary>
    public static byte[] KeyList()
    {
        byte[] configuration = Convert.FromHexString(Rfc9458Example.KeyConfiguration);
        return [0, (byte)configuration.Length, .. configuration];
    }

    /// <summary>A known-length Binary HTTP response (RFC 9292 section 3.1 framing indicator 1).</summary>
    public static byte[] KnownLengthResponse(int status, BinaryHttpField[] fields, string content)
    {
        using MemoryStream fieldSection = new();
        foreach (BinaryHttpField field in fields)
        {
            WriteLengthPrefixed(fieldSection, Encoding.Latin1.GetBytes(field.Name));
            WriteLengthPrefixed(fieldSection, Encoding.Latin1.GetBytes(field.Value));
        }

        using MemoryStream message = new();
        QuicVariableLengthInteger.Write(message, 1);
        QuicVariableLengthInteger.Write(message, status);
        WriteLengthPrefixed(message, fieldSection.ToArray());
        WriteLengthPrefixed(message, Encoding.Latin1.GetBytes(content));
        QuicVariableLengthInteger.Write(message, 0);
        return message.ToArray();
    }

    public async ValueTask<TransferResult> Invoke(ITransferContext context)
    {
        Sent.Add(context);
        return context.Url.Host == RelayHost
            ? await AnswerAsync(context, RelayStatus, RelayContentType, Relay(context.PostData!.Value.ToArray()))
            : await AnswerAsync(context, 200, ObliviousHttpMediaTypes.KeyConfigurations, KeyList());
    }

    private static void WriteLengthPrefixed(MemoryStream destination, byte[] bytes)
    {
        QuicVariableLengthInteger.Write(destination, bytes.Length);
        destination.Write(bytes);
    }

    private static async ValueTask<TransferResult> AnswerAsync(ITransferContext context, int status, string? contentType, byte[] body)
    {
        await context.Output.WriteAsync(body);
        return TransferResult.Success(body.Length) with { Report = new TransferReport { ResponseCode = status, ContentType = contentType } };
    }

    private byte[] Relay(byte[] encapsulatedRequest)
    {
        byte[] header = encapsulatedRequest[..7];
        byte[] encapsulatedKey = encapsulatedRequest[7..39];
        HpkeSuite suite = new(HpkeKem.DhkemX25519HkdfSha256, HpkeKdf.HkdfSha256, HpkeAead.Aes128Gcm);
        using HpkeReceiver receiver = HpkeReceiver.SetUp(suite, encapsulatedKey, gatewayKey, [.. "message/bhttp request"u8, 0, .. header]);
        OpenedRequest = receiver.Open([], encapsulatedRequest[39..]);

        byte[] secret = receiver.Export("message/bhttp response"u8, 16);
        byte[] responseNonce = RandomNumberGenerator.GetBytes(16);
        byte[] pseudorandomKey = HKDF.Extract(HashAlgorithmName.SHA256, secret, [.. encapsulatedKey, .. responseNonce]);
        byte[] key = HKDF.Expand(HashAlgorithmName.SHA256, pseudorandomKey, 16, "key"u8.ToArray());
        byte[] nonce = HKDF.Expand(HashAlgorithmName.SHA256, pseudorandomKey, 12, "nonce"u8.ToArray());
        byte[] sealedText = new byte[BinaryResponse.Length];
        byte[] tag = new byte[16];
        using AesGcm aesGcm = new(key, 16);
        aesGcm.Encrypt(nonce, BinaryResponse, sealedText, tag);
        if (TamperWithResponse)
        {
            tag[0] ^= 1;
        }

        return [.. responseNonce, .. sealedText, .. tag];
    }
}
