using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.Options;
using Libcurl.Options;

namespace Libcurl.Callbacks.Keys;

/// <summary>
/// The setter rows of <c>CURLOPT_SSH_KEYFUNCTION</c>, <c>CURLOPT_SSH_HOSTKEYFUNCTION</c>,
/// <c>CURLOPT_SSL_CTX_FUNCTION</c> and their <c>*DATA</c> options (ADR-0573, ADR-0623). Each SSH function row
/// takes its delegate (<see cref="SshKeyCallback"/>, <see cref="SshHostKeyCallback"/>) or <see langword="null"/>
/// and answers <see cref="CurlCode.BadFunctionArgument"/> for any other value; the engine asks the key callback
/// after the <c>CURLOPT_SSH_KNOWNHOSTS</c> lookup and the host-key callback in place of it. The managed callbacks
/// carry their user data in their closures, so <c>CURLOPT_SSH_KEYDATA</c> and <c>CURLOPT_SSH_HOSTKEYDATA</c> take
/// any value and change nothing. <c>CURLOPT_SSL_CTX_FUNCTION</c> and <c>CURLOPT_SSL_CTX_DATA</c> answer
/// <see cref="CurlCode.NotBuiltIn"/> whatever the value, as libcurl's <c>setopt.c</c> does in the Schannel build,
/// whose backend has no <c>SSLSUPP_SSL_CTX</c>.
/// </summary>
internal static class EasyKeysCallbackSetters
{
    /// <summary>Adds the SSH key and SSL context rows to <paramref name="setters"/>.</summary>
    /// <param name="setters">The rows to add to.</param>
    internal static void AddTo(Dictionary<CurlOption, EasyOptionSetter> setters)
    {
        setters[CurlOption.SshKeyfunction] = new(SetSshKeyFunction);
        setters[CurlOption.SshHostkeyfunction] = new(SetSshHostKeyFunction);
        setters[CurlOption.SshKeydata] = new(TakeUserData);
        setters[CurlOption.SshHostkeydata] = new(TakeUserData);
        setters[CurlOption.SslCtxFunction] = new(AnswerNotBuiltIn);
        setters[CurlOption.SslCtxData] = new(AnswerNotBuiltIn);
    }

    private static CurlCode SetSshKeyFunction(ref EasyOptions options, object? value)
    {
        if (value is not (null or SshKeyCallback))
        {
            return CurlCode.BadFunctionArgument;
        }

        options = options with { Callbacks = options.Callbacks with { SshKey = (SshKeyCallback?)value } };
        return CurlCode.Ok;
    }

    private static CurlCode SetSshHostKeyFunction(ref EasyOptions options, object? value)
    {
        if (value is not (null or SshHostKeyCallback))
        {
            return CurlCode.BadFunctionArgument;
        }

        options = options with { Callbacks = options.Callbacks with { SshHostKey = (SshHostKeyCallback?)value } };
        return CurlCode.Ok;
    }

    private static CurlCode TakeUserData(ref EasyOptions options, object? value) => CurlCode.Ok;

    private static CurlCode AnswerNotBuiltIn(ref EasyOptions options, object? value) => CurlCode.NotBuiltIn;
}
