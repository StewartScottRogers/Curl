namespace Curl.Protobuf.Descriptors;

/// <summary>How an <c>import</c> statement is written.</summary>
public enum ProtoImportKind
{
    /// <summary><c>import "x.proto";</c>: its types are visible to the importing file only.</summary>
    Default,

    /// <summary><c>import public "x.proto";</c>: its types are also visible to every file importing the importing file.</summary>
    Public,

    /// <summary><c>import weak "x.proto";</c>: read like a default import.</summary>
    Weak,
}
