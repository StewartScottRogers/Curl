using Curl.Protobuf.Wire;

namespace Curl.Extension.Protocol.Grpc.Reflection;

/// <summary>Builds <c>grpc.reflection.v1.ServerReflectionRequest</c> messages (reflection.proto, ADR-1149).</summary>
internal static class ServerReflectionRequest
{
    private const int FileByFilenameField = 3;

    private const int FileContainingSymbolField = 4;

    /// <summary>Asks for the file that declares a fully qualified symbol, such as a service, with the files it imports.</summary>
    /// <param name="symbol">The symbol, without a leading dot.</param>
    /// <returns>The request's binary form.</returns>
    public static byte[] FileContainingSymbol(string symbol) => StringRequest(FileContainingSymbolField, symbol);

    /// <summary>Asks for a file by the name another file lists it under as a dependency.</summary>
    /// <param name="fileName">The file's name.</param>
    /// <returns>The request's binary form.</returns>
    public static byte[] FileByFilename(string fileName) => StringRequest(FileByFilenameField, fileName);

    private static byte[] StringRequest(int fieldNumber, string value)
    {
        var writer = new ProtobufWriter();
        writer.WriteStringField(fieldNumber, value);
        return writer.ToArray();
    }
}
