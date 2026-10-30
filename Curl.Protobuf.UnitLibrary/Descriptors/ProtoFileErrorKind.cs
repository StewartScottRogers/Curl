namespace Curl.Protobuf.Descriptors;

/// <summary>Why <see cref="ProtoFileSetReader" /> could not read a set of <c>.proto</c> files.</summary>
public enum ProtoFileErrorKind
{
    /// <summary>The file named to read first does not exist.</summary>
    FileNotFound,

    /// <summary>A file's text is malformed.</summary>
    Syntax,

    /// <summary>An <c>import</c> names a file that does not exist.</summary>
    MissingImport,

    /// <summary>An <c>import</c> leads back to a file that is still being read.</summary>
    ImportCycle,

    /// <summary>A type a field or method names is neither in its file nor in a file that file can see.</summary>
    UnresolvedType,

    /// <summary>A method's request or response names an enum, not a message.</summary>
    NotAMessage,
}
