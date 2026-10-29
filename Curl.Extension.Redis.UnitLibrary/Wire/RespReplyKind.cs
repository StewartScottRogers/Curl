namespace Curl.Extension.Redis.Wire;

/// <summary>The RESP3 reply types ("RESP protocol description"), each named by its first byte.</summary>
internal enum RespReplyKind
{
    /// <summary><c>+</c>, "Simple strings".</summary>
    SimpleString,

    /// <summary><c>-</c>, "Simple errors".</summary>
    SimpleError,

    /// <summary><c>:</c>, "Integers".</summary>
    Integer,

    /// <summary><c>$</c>, "Bulk strings".</summary>
    BulkString,

    /// <summary><c>*</c>, "Arrays".</summary>
    Array,

    /// <summary><c>_</c>, "Nulls", and RESP2's null bulk string and null array (a length of <c>-1</c>).</summary>
    Null,

    /// <summary><c>#</c>, "Booleans".</summary>
    Boolean,

    /// <summary><c>,</c>, "Doubles".</summary>
    Double,

    /// <summary><c>(</c>, "Big numbers".</summary>
    BigNumber,

    /// <summary><c>!</c>, "Bulk errors".</summary>
    BulkError,

    /// <summary><c>=</c>, "Verbatim strings".</summary>
    VerbatimString,

    /// <summary><c>%</c>, "Maps".</summary>
    Map,

    /// <summary><c>|</c>, "Attributes": read and dropped before the reply it describes.</summary>
    Attribute,

    /// <summary><c>~</c>, "Sets".</summary>
    Set,

    /// <summary><c>&gt;</c>, "Pushes".</summary>
    Push,
}
