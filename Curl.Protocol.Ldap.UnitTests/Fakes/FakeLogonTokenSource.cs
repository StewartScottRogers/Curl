namespace Curl.Protocol.Ldap.Fakes;

/// <summary>
/// An <see cref="ILdapLogonTokenSource" /> that plays the logged-on user's security package
/// from a script: every authentication it starts returns <paramref name="tokens" /> in order,
/// then <see langword="null" />, and counts as authenticated once it has returned
/// <paramref name="tokensToAuthenticate" /> of them. It records each start and every
/// challenge it is given. It seals a message by putting <see cref="Signature(int)" /> before
/// it, and unseals one that starts with <see cref="SignatureVersion" /> by taking the
/// signature off.
/// </summary>
/// <param name="tokensToAuthenticate">How many tokens an authentication returns before it is complete.</param>
/// <param name="tokens">The tokens each authentication returns, as hex pairs; <see langword="null" /> for a package that fails there.</param>
public sealed class FakeLogonTokenSource(int tokensToAuthenticate, params string?[] tokens) : ILdapLogonTokenSource
{
    private readonly List<(LdapLogonPackage Package, string TargetName)> starts = [];

    private readonly List<byte[]> challenges = [];

    private readonly int tokensToAuthenticate = tokensToAuthenticate;

    private readonly string?[] tokens = tokens;

    /// <summary>Gets each authentication started, in order.</summary>
    public IReadOnlyList<(LdapLogonPackage Package, string TargetName)> Starts => starts;

    /// <summary>Gets every challenge the authentications were given, the empty first ones included.</summary>
    public IReadOnlyList<byte[]> Challenges => challenges;

    /// <summary>Gets how many authentications have been disposed.</summary>
    public int Disposed { get; private set; }

    /// <inheritdoc />
    public ILdapLogonAuthentication Start(LdapLogonPackage package, string targetName)
    {
        starts.Add((package, targetName));
        return new Authentication(this);
    }

    /// <summary>
    /// The 16-byte signature the fake seals message <paramref name="sequenceNumber" /> with,
    /// as hex pairs: NTLM's version 1, a checksum of <c>5e</c>s, and the sequence number. The
    /// fake's sealed bytes are the message itself.
    /// </summary>
    /// <param name="sequenceNumber">How many messages the authentication sealed before this one.</param>
    /// <returns>The signature.</returns>
    public static string Signature(int sequenceNumber) => $"{SignatureVersion} 5e 5e 5e 5e 5e 5e 5e 5e {sequenceNumber:x2} 00 00 00";

    /// <summary>The first four bytes of every signature, which a message must start with to unwrap.</summary>
    public const string SignatureVersion = "01 00 00 00";

    private const int SignatureLength = 16;

    private sealed class Authentication(FakeLogonTokenSource source) : ILdapLogonAuthentication
    {
        private int returned;

        private int sealedCount;

        private bool disposed;

        public bool IsAuthenticated => returned >= source.tokensToAuthenticate;

        public byte[]? NextToken(ReadOnlySpan<byte> challenge)
        {
            source.challenges.Add(challenge.ToArray());
            string? token = returned < source.tokens.Length ? source.tokens[returned] : null;
            returned++;
            return token is null ? null : Hex.Bytes(token);
        }

        public byte[] Wrap(ReadOnlySpan<byte> message)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return [.. Hex.Bytes(Signature(sealedCount++)), .. message];
        }

        public byte[]? Unwrap(ReadOnlySpan<byte> wrapped)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return wrapped.StartsWith(Hex.Bytes(SignatureVersion)) && wrapped.Length >= SignatureLength ? wrapped[SignatureLength..].ToArray() : null;
        }

        public void Dispose()
        {
            disposed = true;
            source.Disposed++;
        }
    }
}
