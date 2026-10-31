using System.Diagnostics.CodeAnalysis;

namespace Curl.Protobuf.Descriptors;

/// <summary>Reads proto3 <c>.proto</c> text into descriptors.</summary>
public static class ProtoParser
{
    /// <summary>
    /// Reads proto3 text into a <see cref="ProtoFileDescriptor" />; false, with the error and
    /// where it is, when the text is malformed. Never throws on malformed text.
    /// </summary>
    public static bool TryParse(
        string text,
        [NotNullWhen(true)] out ProtoFileDescriptor? file,
        [NotNullWhen(false)] out ProtoSyntaxError? error)
    {
        try
        {
            file = ProtoFileParser.ParseFile(new ProtoTokenCursor(text, ProtoTokenizer.Tokenize(text)));
            error = null;
            return true;
        }
        catch (ProtoSyntaxException exception)
        {
            file = null;
            error = exception.Error;
            return false;
        }
    }
}
