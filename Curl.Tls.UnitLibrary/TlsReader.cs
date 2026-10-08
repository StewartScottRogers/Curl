namespace Curl.Tls;

/// <summary>
/// Reads TLS presentation-language fields (RFC 8446 section 3) from a byte array. The
/// first read that runs past the end records <see cref="TlsAlertDescription.DecodeError" />
/// and every later read returns zero or empty, so a decoder reads its fields in a straight
/// line and checks once, in <see cref="Finish{T}" />. Readers made by
/// <see cref="ReadVector" /> share the failure with the reader they came from.
/// </summary>
internal sealed class TlsReader
{
    private readonly byte[] buffer;

    private readonly int end;

    private readonly TlsReadFailure failure;

    private int position;

    public TlsReader(byte[] buffer)
        : this(buffer, 0, buffer.Length, new TlsReadFailure())
    {
    }

    private TlsReader(byte[] buffer, int start, int end, TlsReadFailure failure)
    {
        this.buffer = buffer;
        position = start;
        this.end = end;
        this.failure = failure;
    }

    /// <summary>Gets a value indicating whether nothing has failed and bytes remain.</summary>
    public bool HasMore => failure.Alert is null && position < end;

    public byte ReadUInt8() => (byte)ReadUnsigned(1);

    public ushort ReadUInt16() => (ushort)ReadUnsigned(2);

    public int ReadUInt24() => (int)ReadUnsigned(3);

    public uint ReadUInt32() => ReadUnsigned(4);

    public byte[] ReadBytes(int count)
    {
        int start = position;
        return Take(count) ? buffer[start..position] : [];
    }

    /// <summary>Reads the bytes left before this reader's end.</summary>
    public byte[] ReadRemaining() => ReadBytes(end - position);

    /// <summary>Reads a variable-length vector's bytes, preceded by a length of <paramref name="lengthBytes" /> bytes.</summary>
    public byte[] ReadOpaque(int lengthBytes) => ReadBytes((int)ReadUnsigned(lengthBytes));

    /// <summary>
    /// Reads a variable-length vector's bytes as <see cref="ReadOpaque" /> does, failing with
    /// <see cref="TlsAlertDescription.DecodeError" /> when the vector is empty: for a field whose range starts at 1.
    /// </summary>
    public byte[] ReadNonEmptyOpaque(int lengthBytes)
    {
        byte[] bytes = ReadOpaque(lengthBytes);
        if (bytes.Length == 0)
        {
            Fail(TlsAlertDescription.DecodeError);
        }

        return bytes;
    }

    /// <summary>Returns a reader over a variable-length vector, preceded by a length of <paramref name="lengthBytes" /> bytes.</summary>
    public TlsReader ReadVector(int lengthBytes)
    {
        int length = (int)ReadUnsigned(lengthBytes);
        int start = position;
        return new TlsReader(buffer, start, Take(length) ? position : start, failure);
    }

    /// <summary>Reads a vector of 16-bit values, preceded by a length of <paramref name="lengthBytes" /> bytes.</summary>
    public IReadOnlyList<ushort> ReadUInt16List(int lengthBytes)
    {
        TlsReader list = ReadVector(lengthBytes);
        List<ushort> values = [];
        while (list.HasMore)
        {
            values.Add(list.ReadUInt16());
        }

        return values;
    }

    /// <summary>Records <paramref name="alert" /> unless a failure is already recorded.</summary>
    public void Fail(TlsAlertDescription alert) => failure.Alert ??= alert;

    /// <summary>Records <see cref="TlsAlertDescription.DecodeError" /> if bytes remain.</summary>
    public void ExpectEnd()
    {
        if (position != end)
        {
            Fail(TlsAlertDescription.DecodeError);
        }
    }

    /// <summary>Requires the reader to be at its end and returns <paramref name="value" />, or the first failure recorded.</summary>
    public TlsDecodeResult<T> Finish<T>(T value)
    {
        ExpectEnd();
        return failure.Alert is { } alert ? TlsDecodeResult<T>.Failure(alert) : TlsDecodeResult<T>.Success(value);
    }

    private uint ReadUnsigned(int byteCount)
    {
        int start = position;
        uint value = 0;
        if (Take(byteCount))
        {
            for (int index = start; index < position; index++)
            {
                value = (value << 8) | buffer[index];
            }
        }

        return value;
    }

    private bool Take(int count)
    {
        if (failure.Alert is not null || end - position < count)
        {
            Fail(TlsAlertDescription.DecodeError);
            return false;
        }

        position += count;
        return true;
    }

    private sealed class TlsReadFailure
    {
        public TlsAlertDescription? Alert { get; set; }
    }
}
