using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// The SASL authenticator of a handler built without one: it picks <c>DIGEST-MD5</c>, which
/// <see cref="LdapOpenLdapSaslBind" /> runs itself, else <c>PLAIN</c>, and no other mechanism.
/// The hosts give the handler the shared authenticator, which knows every mechanism.
/// </summary>
internal sealed class LdapDigestMd5AndPlainSasl : ISaslAuthenticator
{
    private const string Plain = "PLAIN";

    private static readonly string[] Preference = [LdapDigestMd5Bind.Mechanism, Plain];

    /// <inheritdoc />
    public string? ChooseMechanism(SaslRequest request, IReadOnlyList<string> offeredMechanisms) =>
        Array.Find(Preference, mechanism => offeredMechanisms.Contains(mechanism, StringComparer.OrdinalIgnoreCase));

    /// <summary>Starts the <c>PLAIN</c> exchange, the only one begun here: <c>\0user\0password</c> and no answer to a challenge.</summary>
    /// <param name="mechanism">The mechanism chosen, <c>PLAIN</c>.</param>
    /// <param name="request">The credentials to bind with.</param>
    /// <returns>The exchange.</returns>
    public ISaslExchange Begin(string mechanism, SaslRequest request) =>
        new PlainExchange(Encoding.UTF8.GetBytes("\0" + request.Credential!.UserName + "\0" + request.Credential.Password));

    private sealed class PlainExchange(byte[] message) : ISaslExchange
    {
        public string Mechanism => Plain;

        public ValueTask<byte[]?> GetInitialResponseAsync(CancellationToken cancellationToken) => ValueTask.FromResult<byte[]?>(message);

        public ValueTask<byte[]?> RespondAsync(ReadOnlyMemory<byte> challenge, CancellationToken cancellationToken) => ValueTask.FromResult<byte[]?>(null);
    }
}
