namespace Curl.Conformance.Differential.Libcurl.Printf;

/// <summary>
/// One typed argument of a printf scenario, as the tester's <c>pf_in.json</c> writes it:
/// <c>[kind, value]</c> with kind <c>i</c>, <c>l</c> or <c>c</c> (a C <c>int</c> or <c>long</c>,
/// 32 bits on Windows), <c>q</c> (<c>long long</c>), <c>u</c> (<c>unsigned long long</c>),
/// <c>d</c> (a <c>double</c>, written as its 16-hex-digit bit pattern), <c>s</c> (a string or
/// null) or <c>p</c> (a pointer or null).
/// </summary>
/// <param name="Kind">The kind letter.</param>
/// <param name="Number">The integer value, the bit pattern of a <c>d</c>, the bits of a <c>u</c>, or null for a null pointer and an <c>s</c>.</param>
/// <param name="Text">The string of an <c>s</c>; null for a null string and for every other kind.</param>
public sealed record LibcurlPrintfArgument(char Kind, long? Number, string? Text)
{
    private static readonly Dictionary<char, Func<LibcurlPrintfArgument, object?>> CurlArguments = new()
    {
        ['q'] = argument => argument.Number,
        ['p'] = argument => argument.Number,
        ['u'] = argument => unchecked((ulong)argument.Number.GetValueOrDefault()),
        ['d'] = argument => BitConverter.Int64BitsToDouble(argument.Number.GetValueOrDefault()),
        ['s'] = argument => argument.Text,
    };

    /// <summary>
    /// The 8-byte variadic slot the reference <c>curl_msnprintf</c> reads for this argument on x64
    /// Windows, where every variadic argument is one slot and a <c>double</c> is read from its bits.
    /// An <c>s</c> has no slot of its own: the caller passes the address of its UTF-8 copy.
    /// </summary>
    public long Slot => Number ?? 0;

    /// <summary>The argument as the tester's <c>LibTest</c> passed it to Curl's <c>CurlPrintf.MSNPrintf</c>.</summary>
    /// <returns>An <see cref="int"/>, <see cref="long"/>, <see cref="ulong"/>, <see cref="double"/>, <see cref="string"/> or null.</returns>
    public object? ToCurlArgument() => CurlArguments.TryGetValue(Kind, out var convert) ? convert(this) : (int?)Number;
}
