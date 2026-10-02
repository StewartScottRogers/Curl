namespace Curl.Networking;

/// <summary>Why <see cref="ServiceBindingRecordDecoder.Decode" /> refused an HTTPS record's data.</summary>
public enum ServiceBindingFailure
{
    /// <summary>The record decoded.</summary>
    None,

    /// <summary>The data ends before the priority and target name, or within a SvcParam's key and length.</summary>
    Truncated,

    /// <summary>The target name has a label that runs past the data, or is compressed, which RFC 9460 forbids.</summary>
    BadTargetName,

    /// <summary>A SvcParam's length runs past the end of the data.</summary>
    ParameterOverrun,

    /// <summary>
    /// A known SvcParam's value has the wrong shape: an <c>alpn</c> list that is empty or holds an
    /// empty or overrunning identifier, a <c>no-default-alpn</c> with a value, a <c>port</c> that is
    /// not 2 bytes, or an <c>ipv4hint</c> or <c>ipv6hint</c> that is not a whole, non-zero number of addresses.
    /// </summary>
    BadParameterValue,
}
