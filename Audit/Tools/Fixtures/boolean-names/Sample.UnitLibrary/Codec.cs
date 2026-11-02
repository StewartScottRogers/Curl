namespace Sample;

// Fixture for Find-BooleanNameMismatches.ps1 -SelfTest: names renamed to their opposite whose doc
// kept the old sense (PD-401), seven the antonym check must flag and four it must leave alone.
// Nothing here is real.
public static class Codec
{
    /// <returns><see langword="true" /> when the value is valid.</returns>
    public static bool IsInvalid(string value) => value.Length > 0;

    /// <returns><see langword="true" /> when the value is invalid.</returns>
    public static bool IsValid(string value) => value.Length == 0;

    /// <returns><see langword="true" /> when the server refuses the method.</returns>
    public static bool IsAllowed(string method) => method.Length == 0;

    /// <returns><see langword="true" /> when the body was encoded.</returns>
    public static bool IsEncoded(string body) => body.Length > 0;

    /// <summary>Tries to read one frame from the buffer.</summary>
    /// <returns><see langword="true" /> when a frame was there.</returns>
    public static bool TryWrite(byte[] buffer) => buffer.Length > 0;

    /// <summary>Encodes the bytes as base64.</summary>
    public static string Decode(byte[] bytes) => System.Convert.ToBase64String(bytes);

    /// <summary>Disables the option for the rest of the transfer.</summary>
    public static void Enable(string option) => _ = option;

    /// <summary>Closes the stream and releases its handle.</summary>
    public static void Open(System.IO.Stream stream) => stream.Dispose();

    /// <summary>Encodes the bytes as base64.</summary>
    public static string Encode(byte[] bytes) => System.Convert.ToBase64String(bytes);

    /// <summary>Inserts the header before the body.</summary>
    public static string Insert(string header, string body) => header + body;

    /// <summary>Unwraps the frame from the record it was wrapped in.</summary>
    public static byte[] Unwrap(byte[] record) => record;
}
