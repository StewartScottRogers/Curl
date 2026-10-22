namespace Curl.Networking;

/// <summary>Why <see cref="ServiceBindingRecordDecoder.Decode" /> refused an HTTPS record's data.</summary>
public enum ServiceBindingFailure
{
    /// <summary>The record decoded.</summary>
    None,

    /// <summary>The data is 2 bytes or fewer, too short for a priority and a target name; curl returns <c>CURLE_BAD_FUNCTION_ARGUMENT</c>.</summary>
    Truncated,

    /// <summary>
    /// The target name has a label longer than 63 bytes (which also catches compression, which
    /// RFC 9460 forbids) or one that runs to or past the end of the data; curl returns <c>CURLE_OUT_OF_MEMORY</c>.
    /// </summary>
    BadTargetName,

    /// <summary>The target name, up to its first NUL byte, holds a space, a control character or DEL; curl returns <c>CURLE_WEIRD_SERVER_REPLY</c>.</summary>
    TargetNameHasControlCharacter,

    /// <summary>A SvcParam's key is not greater than the one before it; curl returns <c>CURLE_WEIRD_SERVER_REPLY</c>.</summary>
    ParametersOutOfOrder,

    /// <summary>A SvcParam's length runs past the end of the data; curl returns <c>CURLE_WEIRD_SERVER_REPLY</c>.</summary>
    ParameterOverrun,

    /// <summary>An <c>alpn</c> identifier's length runs past the end of the parameter's value; curl returns <c>CURLE_BAD_CONTENT_ENCODING</c>.</summary>
    BadApplicationProtocolList,

    /// <summary>
    /// A known SvcParam's value has the wrong shape: a <c>no-default-alpn</c> with a value, a
    /// <c>port</c> that is not 2 bytes, an empty <c>ech</c>, or an <c>ipv4hint</c> or
    /// <c>ipv6hint</c> that is not a whole, non-zero number of addresses; curl returns <c>CURLE_BAD_FUNCTION_ARGUMENT</c>.
    /// </summary>
    BadParameterValue,
}
