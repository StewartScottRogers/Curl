using System.Security.Cryptography;
using System.Text;

namespace Curl.Conformance.Extended.Xmpp;

/// <summary>
/// The server side of one SCRAM authentication (RFC 5802, 3 and 5; RFC 7677) with a fixed salt and iteration count:
/// the server-first message for curl's client-first message, and the check of curl's client proof.
/// </summary>
/// <param name="hash">SHA-1 for SCRAM-SHA-1, SHA-256 for SCRAM-SHA-256.</param>
/// <param name="password">The password the keys derive from.</param>
/// <param name="clientFirst">curl's client-first message, GS2 header included.</param>
internal sealed class XmppReferenceScram(HashAlgorithmName hash, string password, string clientFirst)
{
    private const int Iterations = 4096;

    private static readonly byte[] Salt = "curl-conformance-salt"u8.ToArray();

    // The client-first-message-bare: everything after the GS2 header "n,,".
    private readonly string clientFirstBare = clientFirst[3..];

    /// <summary>Gets the server-first message: curl's nonce with the server's appended, the salt and the iteration count.</summary>
    internal string ServerFirst => $"r={clientFirstBare.Split(",r=")[1]}reference-nonce,s={Convert.ToBase64String(Salt)},i={Iterations}";

    /// <summary>Checks curl's client-final message and gives the server signature (RFC 5802, 3) it answers with.</summary>
    /// <param name="clientFinal">curl's client-final message.</param>
    /// <param name="proofVerified">Whether curl's client proof matched the password.</param>
    /// <returns>The server signature.</returns>
    internal byte[] ServerSignature(string clientFinal, out bool proofVerified)
    {
        int proofStart = clientFinal.LastIndexOf(",p=", StringComparison.Ordinal);
        byte[] authMessage = Encoding.UTF8.GetBytes($"{clientFirstBare},{ServerFirst},{clientFinal[..proofStart]}");
        byte[] saltedPassword = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), Salt, Iterations, hash, CryptographicOperations.HashData(hash, []).Length);
        byte[] clientKey = CryptographicOperations.HmacData(hash, saltedPassword, "Client Key"u8);
        byte[] clientSignature = CryptographicOperations.HmacData(hash, CryptographicOperations.HashData(hash, clientKey), authMessage);
        byte[] proof = [.. clientKey.Zip(clientSignature, (key, signature) => (byte)(key ^ signature))];
        proofVerified = clientFinal[(proofStart + 3)..] == Convert.ToBase64String(proof);
        return CryptographicOperations.HmacData(hash, CryptographicOperations.HmacData(hash, saltedPassword, "Server Key"u8), authMessage);
    }
}
