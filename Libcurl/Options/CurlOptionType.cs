namespace Libcurl.Options;

/// <summary>
/// libcurl's <c>curl_easytype</c> (<c>CURLOT_*</c>, <c>include/curl/options.h</c>): the type of
/// value an option takes, with upstream's numbers.
/// </summary>
public enum CurlOptionType
{
    /// <summary><c>CURLOT_LONG</c> (0): a <c>long</c> in a range of values; set as <see cref="long"/> or <see cref="int"/>.</summary>
    Long = 0,

    /// <summary><c>CURLOT_VALUES</c> (1): a <c>long</c> from a defined set or bitmask; set as <see cref="long"/>, <see cref="int"/> or an <see cref="Enum"/>.</summary>
    Values = 1,

    /// <summary><c>CURLOT_OFF_T</c> (2): a <c>curl_off_t</c>; set as <see cref="long"/> or <see cref="int"/>.</summary>
    OffT = 2,

    /// <summary><c>CURLOT_OBJECT</c> (3): a pointer to an object; set as any value but a <see cref="Delegate"/>, or <see langword="null"/>.</summary>
    Object = 3,

    /// <summary><c>CURLOT_STRING</c> (4): a null-terminated string; set as a <see cref="string"/> or <see langword="null"/>.</summary>
    String = 4,

    /// <summary><c>CURLOT_SLIST</c> (5): a <c>struct curl_slist</c>; set as an <see cref="IEnumerable{T}"/> of <see cref="string"/>, or <see langword="null"/>.</summary>
    SList = 5,

    /// <summary><c>CURLOT_CBPTR</c> (6): a pointer passed as-is to a callback; set as any value but a <see cref="Delegate"/>, or <see langword="null"/>.</summary>
    CallbackPointer = 6,

    /// <summary><c>CURLOT_BLOB</c> (7): a <c>struct curl_blob</c>; set as a <see cref="byte"/> array, a <see cref="ReadOnlyMemory{T}"/> of <see cref="byte"/>, or <see langword="null"/>.</summary>
    Blob = 7,

    /// <summary><c>CURLOT_FUNCTION</c> (8): a function pointer; set as a <see cref="Delegate"/> or <see langword="null"/>.</summary>
    Function = 8,
}
