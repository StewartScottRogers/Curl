using System.Net.Security;
using System.Runtime.CompilerServices;

namespace Curl.Networking.Fakes;

/// <summary>
/// An <see cref="ICipherSuitesPolicyFactory" /> that records the suites it is given and
/// returns one policy made without its constructor, so a test on any platform, Windows
/// included, can follow that policy into the handshake options.
/// </summary>
internal sealed class RecordingCipherSuitesPolicyFactory : ICipherSuitesPolicyFactory
{
    /// <summary>Gets the policy every call returns.</summary>
    public CipherSuitesPolicy Policy { get; } =
        (CipherSuitesPolicy)RuntimeHelpers.GetUninitializedObject(typeof(CipherSuitesPolicy));

    /// <summary>Gets the suites of the last call, or <see langword="null" /> before any.</summary>
    public TlsCipherSuite[]? Suites { get; private set; }

    /// <inheritdoc />
    public CipherSuitesPolicy? Create(IEnumerable<TlsCipherSuite> suites)
    {
        Suites = [.. suites];
        return Policy;
    }
}
