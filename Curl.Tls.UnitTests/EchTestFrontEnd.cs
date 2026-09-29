using System.Buffers.Binary;
using Curl.Cryptography;

namespace Curl.Tls;

/// <summary>
/// An ECH client-facing server (RFC 9849 section 7.1) for the tests: it opens each outer
/// ClientHello's payload with the config's private key, checking the associated data, the
/// padding and the inner hello's <c>inner</c> extension, and returns the inner hello, its
/// legacy session ID restored, for the backend <see cref="Tls13TestServer" /> to answer.
/// </summary>
internal sealed class EchTestFrontEnd(EchTestConfig config) : IDisposable
{
    private HpkeContext? context;

    public List<ClientHello> OuterHellos { get; } = [];

    public List<ClientHello> InnerHellos { get; } = [];

    /// <summary>Gets the length of each decrypted <c>EncodedClientHelloInner</c>, padding included.</summary>
    public List<int> EncodedInnerLengths { get; } = [];

    public static string? ServerNameOf(ClientHello hello) =>
        hello.Extensions.FirstOrDefault(extension => extension.Type == TlsExtensionType.ServerName) is { } serverName
            ? ServerNameExtension.DecodeHostName(serverName.Data).Value
            : null;

    public byte[] Decrypt(byte[] outerMessage)
    {
        ClientHello outer = ClientHello.Decode(outerMessage[HandshakeMessage.HeaderLength..]).Value;
        OuterHellos.Add(outer);
        TlsReader reader = new(outer.Extensions.Single(extension => extension.Type == TlsExtensionType.EncryptedClientHello).Data);
        Assert.AreEqual(0, reader.ReadUInt8());
        HpkeKdf kdf = (HpkeKdf)reader.ReadUInt16();
        HpkeAead aead = (HpkeAead)reader.ReadUInt16();
        Assert.AreEqual(config.ConfigId, reader.ReadUInt8());
        byte[] encapsulatedKey = reader.ReadOpaque(2);
        byte[] payload = reader.ReadOpaque(2);
        Assert.IsTrue(reader.Finish(true).Succeeded);
        OpenContext(kdf, aead, encapsulatedKey);

        byte[] encodedInner = new byte[payload.Length - HpkeContext.TagSize];
        Assert.IsTrue(context!.TryOpen(AssociatedData(outer, payload.Length), payload, encodedInner));
        EncodedInnerLengths.Add(encodedInner.Length);
        Assert.AreEqual(0, encodedInner.Length % 32);
        int helloLength = ClientHelloBodyLength(encodedInner);
        Assert.IsTrue(encodedInner.AsSpan(helloLength).IndexOfAnyExcept((byte)0) < 0);
        ClientHello inner = ClientHello.Decode(encodedInner[..helloLength]).Value;
        Assert.IsEmpty(inner.LegacySessionId);
        inner = inner with { LegacySessionId = outer.LegacySessionId };
        CollectionAssert.AreEqual(new byte[] { 1 }, inner.Extensions.Single(extension => extension.Type == TlsExtensionType.EncryptedClientHello).Data);
        InnerHellos.Add(inner);
        return inner.Encode();
    }

    public void Dispose() => context?.Dispose();

    /// <summary>The first hello's <c>enc</c> sets up the context; after a HelloRetryRequest it is empty and the context runs on (section 7.1.1).</summary>
    private void OpenContext(HpkeKdf kdf, HpkeAead aead, byte[] encapsulatedKey)
    {
        if (context is not null)
        {
            Assert.IsEmpty(encapsulatedKey);
            return;
        }

        byte[] info = [.. "tls ech"u8, 0x00, .. config.Encode()];
        Assert.IsTrue(Hpke.TrySetupBaseRecipient(config.Kem, kdf, aead, encapsulatedKey, config.PrivateKey, info, out context));
    }

    private static byte[] AssociatedData(ClientHello outer, int payloadLength)
    {
        TlsExtension original = outer.Extensions.Single(extension => extension.Type == TlsExtensionType.EncryptedClientHello);
        byte[] zeroed = [.. original.Data];
        zeroed.AsSpan(zeroed.Length - payloadLength).Clear();
        ClientHello withoutPayload = outer with
        {
            Extensions = [.. outer.Extensions.Select(extension => extension == original ? new TlsExtension(extension.Type, zeroed) : extension)],
        };
        return withoutPayload.Encode()[HandshakeMessage.HeaderLength..];
    }

    /// <summary>The length of the ClientHello at the start of <paramref name="body" />, before its padding.</summary>
    private static int ClientHelloBodyLength(byte[] body)
    {
        int position = 2 + ClientHello.RandomLength;
        position += 1 + body[position];
        position += 2 + BinaryPrimitives.ReadUInt16BigEndian(body.AsSpan(position));
        position += 1 + body[position];
        return position + 2 + BinaryPrimitives.ReadUInt16BigEndian(body.AsSpan(position));
    }
}
