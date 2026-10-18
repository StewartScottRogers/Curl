namespace Curl.Conformance.LibTest;

/// <summary>The code a string of <see cref="Lib1597.Cases"/> expects in <c>lib1597.c</c>.</summary>
public enum Lib1597Expectation
{
    /// <summary><c>CURLE_OK</c>.</summary>
    Ok,

    /// <summary><c>CURLE_BAD_FUNCTION_ARGUMENT</c>.</summary>
    BadArgument,

    /// <summary><c>CURLE_UNSUPPORTED_PROTOCOL</c>.</summary>
    Unsupported,

    /// <summary><c>httpcode</c>: <c>CURLE_OK</c> when HTTP is enabled, else <c>CURLE_UNSUPPORTED_PROTOCOL</c>.</summary>
    Http,

    /// <summary><c>httpscode</c>: <c>CURLE_OK</c> when HTTPS is enabled, else <c>CURLE_UNSUPPORTED_PROTOCOL</c>.</summary>
    Https,
}
