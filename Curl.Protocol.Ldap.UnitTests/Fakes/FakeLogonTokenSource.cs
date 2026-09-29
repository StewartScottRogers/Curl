namespace Curl.Protocol.Ldap.Fakes;

/// <summary>
/// An <see cref="ILdapLogonTokenSource" /> that plays the logged-on user's security package
/// from a script: every authentication it starts returns <paramref name="tokens" /> in order,
/// then <see langword="null" />, and counts as authenticated once it has returned
/// <paramref name="tokensToAuthenticate" /> of them. It records each start and every
/// challenge it is given.
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

    private sealed class Authentication(FakeLogonTokenSource source) : ILdapLogonAuthentication
    {
        private int returned;

        public bool IsAuthenticated => returned >= source.tokensToAuthenticate;

        public byte[]? NextToken(ReadOnlySpan<byte> challenge)
        {
            source.challenges.Add(challenge.ToArray());
            string? token = returned < source.tokens.Length ? source.tokens[returned] : null;
            returned++;
            return token is null ? null : Hex.Bytes(token);
        }

        public void Dispose() => source.Disposed++;
    }
}
