using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Xmpp.Client;

/// <summary>
/// One SCRAM authentication's client side (RFC 5802, 3 and 5) over <paramref name="hash"/>, without channel binding
/// (GS2 header <c>n,,</c>): the client-first message, the client proof answering the server-first message, and the
/// check of the server signature, which arrives in <c>&lt;success/&gt;</c> or, from some servers, in a second challenge.
/// The password is used as UTF-8 without SASLprep (ADR-1077).
/// </summary>
/// <param name="hash">The hash function, SHA-1 or SHA-256.</param>
/// <param name="userName">The user name, escaped as RFC 5802's <c>saslname</c>.</param>
/// <param name="password">The password.</param>
/// <param name="clientNonce">The client nonce, printable and without a comma.</param>
internal sealed class XmppScramExchange(HashAlgorithmName hash, string userName, string password, string clientNonce) : IXmppSaslExchange
{
    /// <summary>The condition a server signature that does not match names.</summary>
    internal const string SignatureMismatch = "server signature mismatch";

    /// <summary>The condition a server-first message the client cannot use names.</summary>
    internal const string MalformedServerFirst = "malformed SCRAM server-first-message";

    private const string Gs2Header = "n,,";

    private readonly string clientFirstBare = $"n={userName.Replace("=", "=3D", StringComparison.Ordinal).Replace(",", "=2C", StringComparison.Ordinal)},r={clientNonce}";

    private byte[]? expectedServerSignature;

    private bool verified;

    /// <inheritdoc />
    public string InitialResponse => Gs2Header + clientFirstBare;

    /// <summary>Gives a fresh client nonce: 18 random bytes, base64 encoded.</summary>
    internal static string CreateClientNonce() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));

    /// <inheritdoc />
    public string Respond(string challenge)
    {
        if (expectedServerSignature is null)
        {
            return ClientFinal(challenge);
        }

        Verify(challenge);
        return string.Empty;
    }

    /// <inheritdoc />
    public void Complete(string additionalData)
    {
        if (!verified)
        {
            Verify(additionalData);
        }
    }

    private string ClientFinal(string serverFirst)
    {
        Dictionary<char, string> attributes = AttributesOf(serverFirst);
        string nonce = ServerNonce(attributes);
        byte[] salt = Base64Attribute(attributes, 's') ?? throw Failed(MalformedServerFirst);
        int iterations = Iterations(attributes);
        string withoutProof = $"c={Convert.ToBase64String(Encoding.ASCII.GetBytes(Gs2Header))},r={nonce}";
        byte[] authMessage = Encoding.UTF8.GetBytes($"{clientFirstBare},{serverFirst},{withoutProof}");
        byte[] saltedPassword = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, hash, CryptographicOperations.HashData(hash, []).Length);
        byte[] clientKey = CryptographicOperations.HmacData(hash, saltedPassword, "Client Key"u8);
        byte[] clientSignature = CryptographicOperations.HmacData(hash, CryptographicOperations.HashData(hash, clientKey), authMessage);
        byte[] proof = [.. clientKey.Zip(clientSignature, (key, signature) => (byte)(key ^ signature))];
        expectedServerSignature = CryptographicOperations.HmacData(hash, CryptographicOperations.HmacData(hash, saltedPassword, "Server Key"u8), authMessage);
        return $"{withoutProof},p={Convert.ToBase64String(proof)}";
    }

    private string ServerNonce(Dictionary<char, string> attributes) =>
        attributes.TryGetValue('r', out string? nonce) && nonce.Length > clientNonce.Length && nonce.StartsWith(clientNonce, StringComparison.Ordinal)
            ? nonce
            : throw Failed(MalformedServerFirst);

    private static int Iterations(Dictionary<char, string> attributes) =>
        attributes.TryGetValue('i', out string? text) && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int iterations) && iterations > 0
            ? iterations
            : throw Failed(MalformedServerFirst);

    private void Verify(string serverFinal)
    {
        Dictionary<char, string> attributes = AttributesOf(serverFinal);
        if (attributes.TryGetValue('e', out string? error))
        {
            throw Failed(error);
        }

        byte[]? signature = Base64Attribute(attributes, 'v');
        if (expectedServerSignature is null || signature is null || !CryptographicOperations.FixedTimeEquals(signature, expectedServerSignature))
        {
            throw Failed(SignatureMismatch);
        }

        verified = true;
    }

    private static byte[]? Base64Attribute(Dictionary<char, string> attributes, char name)
    {
        try
        {
            return attributes.TryGetValue(name, out string? text) ? Convert.FromBase64String(text) : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static Dictionary<char, string> AttributesOf(string message)
    {
        Dictionary<char, string> attributes = [];
        foreach (string attribute in message.Split(','))
        {
            if (attribute.Length > 1 && attribute[1] == '=')
            {
                attributes.TryAdd(attribute[0], attribute[2..]);
            }
        }

        return attributes;
    }

    private static XmppClientException Failed(string condition) =>
        new(CurlExitCode.LoginDenied, XmppClientMessages.AuthenticationFailed(condition));
}
