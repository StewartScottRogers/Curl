using System.Security.Cryptography;
using System.Text;

namespace Curl.Extended.Protocol.Kafka.Wire.KafkaSasl;

/// <summary>
/// The client side of one SCRAM exchange (RFC 5802, RFC 7677) built on the base class library's PBKDF2 and HMAC: the
/// client-first message, the client-final message with its proof once the server-first message arrived, and the check of
/// the server's signature. The password is never kept beyond the proof's computation and never written anywhere.
/// </summary>
internal sealed class KafkaScramClient(HashAlgorithmName hash, string userName, string password, string clientNonce)
{
    private const string Gs2Header = "n,,";
    private const string ChannelBinding = "c=biws";

    private string clientFirstBare = string.Empty;
    private string authMessage = string.Empty;
    private byte[] serverKey = [];

    /// <summary>Gives the client-first message: the GS2 header <c>n,,</c> and <c>n=</c> the user name, <c>r=</c> the client nonce.</summary>
    internal byte[] ClientFirst()
    {
        clientFirstBare = $"n={userName.Replace("=", "=3D", StringComparison.Ordinal).Replace(",", "=2C", StringComparison.Ordinal)},r={clientNonce}";
        return Encoding.UTF8.GetBytes(Gs2Header + clientFirstBare);
    }

    /// <summary>
    /// Answers the server-first message (<c>r=</c> nonce, <c>s=</c> salt, <c>i=</c> iterations) with the client-final message.
    /// A message that is malformed or whose nonce does not start with the client's is a <see cref="FormatException"/>.
    /// </summary>
    internal byte[] ClientFinal(ReadOnlySpan<byte> serverFirstBytes)
    {
        string serverFirst = Encoding.UTF8.GetString(serverFirstBytes);
        (string nonce, byte[] salt, int iterations) = ParseServerFirst(Attributes(serverFirst));
        string clientFinalWithoutProof = $"{ChannelBinding},r={nonce}";
        authMessage = $"{clientFirstBare},{serverFirst},{clientFinalWithoutProof}";
        byte[] salted = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, hash, HashSize());
        byte[] clientKey = Hmac(salted, "Client Key"u8);
        byte[] storedKey = CryptographicOperations.HashData(hash, clientKey);
        byte[] signature = Hmac(storedKey, Encoding.UTF8.GetBytes(authMessage));
        serverKey = Hmac(salted, "Server Key"u8);
        for (int index = 0; index < clientKey.Length; index++)
        {
            clientKey[index] ^= signature[index];
        }

        return Encoding.UTF8.GetBytes($"{clientFinalWithoutProof},p={Convert.ToBase64String(clientKey)}");
    }

    /// <summary>Checks the server-final message's <c>v=</c> signature; a missing or wrong one is a <see cref="FormatException"/>.</summary>
    internal void VerifyServerFinal(ReadOnlySpan<byte> serverFinalBytes)
    {
        Dictionary<char, string> attributes = Attributes(Encoding.UTF8.GetString(serverFinalBytes));
        byte[] expected = Hmac(serverKey, Encoding.UTF8.GetBytes(authMessage));
        if (!attributes.TryGetValue('v', out string? signatureText)
            || !CryptographicOperations.FixedTimeEquals(expected, Convert.FromBase64String(signatureText)))
        {
            throw new FormatException("The Kafka broker's SCRAM server signature is missing or wrong");
        }
    }

    private (string Nonce, byte[] Salt, int Iterations) ParseServerFirst(Dictionary<char, string> attributes)
    {
        string? nonce = attributes.GetValueOrDefault('r');
        string? saltText = attributes.GetValueOrDefault('s');
        if (nonce is null || saltText is null || !nonce.StartsWith(clientNonce, StringComparison.Ordinal)
            || !int.TryParse(attributes.GetValueOrDefault('i'), out int iterations) || iterations < 1)
        {
            throw new FormatException("The Kafka broker's SCRAM server-first message is malformed");
        }

        return (nonce, Convert.FromBase64String(saltText), iterations);
    }

    private static Dictionary<char, string> Attributes(string message)
    {
        Dictionary<char, string> attributes = [];
        foreach (string part in message.Split(','))
        {
            if (part.Length >= 2 && part[1] == '=')
            {
                attributes[part[0]] = part[2..];
            }
        }

        return attributes;
    }

    private int HashSize() => hash == HashAlgorithmName.SHA512 ? 64 : 32;

    private byte[] Hmac(byte[] key, ReadOnlySpan<byte> data) => hash == HashAlgorithmName.SHA512 ? HMACSHA512.HashData(key, data) : HMACSHA256.HashData(key, data);
}
