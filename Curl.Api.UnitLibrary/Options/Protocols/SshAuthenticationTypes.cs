namespace Curl.Api.Options.Protocols;

/// <summary>
/// The <c>CURLSSH_AUTH_*</c> bits <c>CURLOPT_SSH_AUTH_TYPES</c> accepts: the methods SSH may
/// authenticate with. Each member's value is libcurl's bit, in the 32 bits a <c>long</c>
/// carries on every platform.
/// </summary>
[Flags]
public enum SshAuthenticationTypes : uint
{
    /// <summary><c>CURLSSH_AUTH_NONE</c> (0): no authentication.</summary>
    None = 0,

    /// <summary><c>CURLSSH_AUTH_PUBLICKEY</c> (1 &lt;&lt; 0): a public and private key pair.</summary>
    PublicKey = 1u << 0,

    /// <summary><c>CURLSSH_AUTH_PASSWORD</c> (1 &lt;&lt; 1): a password.</summary>
    Password = 1u << 1,

    /// <summary><c>CURLSSH_AUTH_HOST</c> (1 &lt;&lt; 2): host-based authentication.</summary>
    Host = 1u << 2,

    /// <summary><c>CURLSSH_AUTH_KEYBOARD</c> (1 &lt;&lt; 3): keyboard-interactive.</summary>
    Keyboard = 1u << 3,

    /// <summary><c>CURLSSH_AUTH_AGENT</c> (1 &lt;&lt; 4): keys held by an SSH agent.</summary>
    Agent = 1u << 4,

    /// <summary><c>CURLSSH_AUTH_GSSAPI</c> (1 &lt;&lt; 5): GSS-API (Kerberos).</summary>
    GssApi = 1u << 5,

    /// <summary><c>CURLSSH_AUTH_ANY</c> (<c>~0</c>, also <c>CURLSSH_AUTH_DEFAULT</c>): every method; libcurl's default.</summary>
    Any = ~0u,
}
