namespace Curl.Extension.CompressionDictionary.Brotli;

/// <summary>Decodes a compressed meta-block: its header (RFC 7932 section 9.2), then its commands (section 9.3).</summary>
internal sealed class BrotliCompressedMetaBlock
{
    private readonly BrotliBitReader _reader;
    private readonly BrotliOutput _output;
    private readonly BrotliDistances _distances;
    private readonly int _maximumBackwardDistance;
    private readonly BrotliBlockSwitch _literalBlocks;
    private readonly BrotliBlockSwitch _commandBlocks;
    private readonly BrotliBlockSwitch _distanceBlocks;
    private readonly int _postfixBits;
    private readonly int _directCount;
    private readonly int[] _contextModes;
    private readonly byte[] _literalContextMap;
    private readonly byte[] _distanceContextMap;
    private readonly BrotliPrefixCode[] _literalCodes;
    private readonly BrotliPrefixCode[] _commandCodes;
    private readonly BrotliPrefixCode[] _distanceCodes;

    /// <summary>Reads the meta-block header after MLEN and ISUNCOMPRESSED.</summary>
    public BrotliCompressedMetaBlock(BrotliBitReader reader, BrotliOutput output, BrotliDistances distances, int maximumBackwardDistance)
    {
        _reader = reader;
        _output = output;
        _distances = distances;
        _maximumBackwardDistance = maximumBackwardDistance;
        _literalBlocks = BrotliBlockSwitch.Read(reader);
        _commandBlocks = BrotliBlockSwitch.Read(reader);
        _distanceBlocks = BrotliBlockSwitch.Read(reader);
        _postfixBits = reader.ReadBits(2);
        _directCount = reader.ReadBits(4) << _postfixBits;
        _contextModes = new int[_literalBlocks.TypeCount];
        for (var type = 0; type < _contextModes.Length; type++)
        {
            _contextModes[type] = reader.ReadBits(2);
        }

        _literalContextMap = BrotliContextMap.Read(reader, 64 * _literalBlocks.TypeCount, out var literalTreeCount);
        _distanceContextMap = BrotliContextMap.Read(reader, 4 * _distanceBlocks.TypeCount, out var distanceTreeCount);
        _literalCodes = ReadCodes(literalTreeCount, 256);
        _commandCodes = ReadCodes(_commandBlocks.TypeCount, BrotliLengthCodes.CommandAlphabetSize);
        _distanceCodes = ReadCodes(distanceTreeCount, 16 + _directCount + (48 << _postfixBits));
    }

    /// <summary>Runs commands until the meta-block's <paramref name="length"/> bytes are out.</summary>
    public void Decode(int length)
    {
        var end = _output.Length + length;
        _output.Reserve(length);
        while (true)
        {
            _commandBlocks.CountDown(_reader);
            var command = BrotliLengthCodes.ReadCommand(_commandCodes[_commandBlocks.CurrentType].Decode(_reader), _reader);
            InsertLiterals(command.InsertLength, end);
            if (_output.Length == end)
            {
                return;
            }

            Copy(command, end);
            if (_output.Length == end)
            {
                return;
            }
        }
    }

    private BrotliPrefixCode[] ReadCodes(int count, int alphabetSize)
    {
        var codes = new BrotliPrefixCode[count];
        for (var index = 0; index < count; index++)
        {
            codes[index] = BrotliPrefixCodeReader.Read(_reader, alphabetSize);
        }

        return codes;
    }

    private void InsertLiterals(int count, int end)
    {
        CheckFits(count, end);
        for (var index = 0; index < count; index++)
        {
            _literalBlocks.CountDown(_reader);
            var type = _literalBlocks.CurrentType;
            var context = BrotliLiteralContext.Of(_contextModes[type], _output.ByteBack(1), _output.ByteBack(2));
            _output.Append((byte)_literalCodes[_literalContextMap[(type * 64) + context]].Decode(_reader));
        }
    }

    // Section 4: a distance past the history reaches the prefix dictionary (RFC 9842 section 4), then the static dictionary.
    private void Copy(BrotliCommand command, int end)
    {
        var code = command.UsesLastDistance ? 0 : ReadDistanceCode(command.CopyLength);
        var distance = _distances.Resolve(code, _postfixBits, _directCount, _reader);
        var reachable = Math.Min(_output.Length, _maximumBackwardDistance);
        if (distance <= reachable)
        {
            CheckFits(command.CopyLength, end);
            _output.CopyBack(distance, command.CopyLength);
            _distances.RememberUnlessLast(code, distance);
        }
        else if (distance - reachable <= _output.PrefixDictionaryLength)
        {
            CheckFits(command.CopyLength, end);
            _output.CopyFromPrefixDictionary(distance - reachable, command.CopyLength);
            _distances.RememberUnlessLast(code, distance);
        }
        else
        {
            BrotliStaticDictionary.WriteWord(_output, command.CopyLength, distance - reachable - _output.PrefixDictionaryLength - 1);
            CheckFits(0, end);
        }
    }

    private int ReadDistanceCode(int copyLength)
    {
        _distanceBlocks.CountDown(_reader);
        var context = Math.Min(copyLength, 5) - 2;
        return _distanceCodes[_distanceContextMap[(_distanceBlocks.CurrentType * 4) + context]].Decode(_reader);
    }

    private void CheckFits(int count, int end)
    {
        if (count > end - _output.Length)
        {
            throw new BrotliFormatException("A Brotli command runs past the end of its meta-block (RFC 7932 section 9.3).");
        }
    }
}
