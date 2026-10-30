using System.Net;
using Curl.HttpDatagram;

namespace Curl.Extension.Masque.Wire;

/// <summary>
/// Reads the fields of one CONNECT-IP capsule's value in order, refusing it as malformed
/// when a field runs past the end (RFC 9484 section 4.7).
/// </summary>
internal ref struct CapsuleValueReader
{
    private readonly string capsuleName;
    private ReadOnlySpan<byte> remaining;

    /// <summary>
    /// Initializes a new instance of the <see cref="CapsuleValueReader" /> struct.
    /// </summary>
    /// <param name="value">The capsule's value.</param>
    /// <param name="capsuleName">The capsule's name, for the error message.</param>
    public CapsuleValueReader(ReadOnlySpan<byte> value, string capsuleName)
    {
        remaining = value;
        this.capsuleName = capsuleName;
    }

    /// <summary>Gets a value indicating whether every byte of the value has been read.</summary>
    public readonly bool AtEnd => remaining.IsEmpty;

    /// <summary>Reads a variable-length integer.</summary>
    /// <returns>The integer.</returns>
    public ulong ReadVariableLengthInteger()
    {
        if (!HttpDatagramVariableLengthInteger.TryRead(remaining, out var value, out var consumed))
        {
            throw Malformed("ends inside a variable-length integer");
        }

        remaining = remaining[consumed..];
        return value;
    }

    /// <summary>Reads one byte.</summary>
    /// <returns>The byte.</returns>
    public byte ReadByte() => Take(1)[0];

    /// <summary>Reads an IP Version byte and returns the address length it calls for.</summary>
    /// <returns>4 for IPv4, 16 for IPv6.</returns>
    public int ReadIpVersion() => ReadByte() switch
    {
        4 => 4,
        6 => 16,
        var version => throw Malformed($"names IP version {version}"),
    };

    /// <summary>Reads an IP address of the given length.</summary>
    /// <param name="length">4 or 16.</param>
    /// <returns>The address.</returns>
    public IPAddress ReadAddress(int length) => new(Take(length));

    /// <summary>Makes the exception for a malformed capsule.</summary>
    /// <param name="reason">What is wrong with it.</param>
    /// <returns>The exception to throw.</returns>
    public readonly MasqueException Malformed(string reason) => MalformedCapsule(capsuleName, reason);

    /// <summary>Makes the exception for a malformed capsule.</summary>
    /// <param name="capsuleName">The capsule's name.</param>
    /// <param name="reason">What is wrong with it.</param>
    /// <returns>The exception to throw.</returns>
    public static MasqueException MalformedCapsule(string capsuleName, string reason) => new($"MASQUE proxy sent a malformed {capsuleName} capsule: it {reason}");

    private ReadOnlySpan<byte> Take(int length)
    {
        if (remaining.Length < length)
        {
            throw Malformed("ends inside a field");
        }

        var taken = remaining[..length];
        remaining = remaining[length..];
        return taken;
    }
}
