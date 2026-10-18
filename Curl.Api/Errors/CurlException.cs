using Curl.Api.Codes;

namespace Curl.Api.Errors;

/// <summary>
/// The one exception every failure in <c>Curl.Api</c> throws: the <see cref="CurlCode"/> the
/// engine returned and the text that explains it.
/// </summary>
public sealed class CurlException : Exception
{
    /// <summary>Creates the exception for a failed call.</summary>
    /// <param name="code">The libcurl result; never <see cref="CurlCode.Ok"/>.</param>
    /// <param name="message">The text that explains the failure.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="code"/> is <see cref="CurlCode.Ok"/>.</exception>
    public CurlException(CurlCode code, string message)
        : base(message)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(code, CurlCode.Ok);
        Code = code;
    }

    /// <summary>The libcurl result the failed call returned.</summary>
    public CurlCode Code { get; }

    /// <summary>
    /// Returns when <paramref name="code"/> is <see cref="CurlCode.Ok"/>, and otherwise throws a
    /// <see cref="CurlException"/> for it.
    /// </summary>
    /// <param name="code">The result of the call.</param>
    /// <param name="errorText">
    /// The handle's <see cref="EngineContracts.IEasyTransfer.ErrorText"/>; when empty the message is
    /// <see cref="CurlCodeText.Of"/> of the code.
    /// </param>
    /// <exception cref="CurlException"><paramref name="code"/> is not <see cref="CurlCode.Ok"/>.</exception>
    public static void ThrowIfFailed(CurlCode code, string errorText)
    {
        ArgumentNullException.ThrowIfNull(errorText);
        if (code != CurlCode.Ok)
        {
            throw new CurlException(code, errorText.Length > 0 ? errorText : CurlCodeText.Of(code));
        }
    }
}
