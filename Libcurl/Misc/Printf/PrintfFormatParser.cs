namespace Libcurl.Misc.Printf;

/// <summary>
/// Parses a format as <c>lib/mprintf.c</c>'s <c>parsefmt</c> does: into output segments and the type
/// each argument is read as, answering <see langword="null"/> for every format <c>parsefmt</c> refuses
/// (a positional and a sequential conversion mixed, an argument left unused, more than 128 arguments
/// or segments, a width or precision past <c>INT_MAX</c>, both kinds of precision on one conversion).
/// </summary>
internal sealed class PrintfFormatParser
{
    /// <summary><c>MAX_PARAMETERS</c>: how many arguments one format may read.</summary>
    internal const int MaxParameters = 128;

    /// <summary><c>MAX_SEGMENTS</c>: how many output segments one format may have.</summary>
    internal const int MaxSegments = 128;

    // A conversion with no "n$" takes the next argument; a malformed or mixed-in "n$" is invalid.
    private const int NoPosition = -1;
    private const int InvalidPosition = -2;

    private static readonly Dictionary<byte, PrintfFlags> FlagCharacters = new()
    {
        [(byte)' '] = PrintfFlags.Space,
        [(byte)'+'] = PrintfFlags.ShowSign,
        [(byte)'#'] = PrintfFlags.Alternate,
        [(byte)'h'] = PrintfFlags.Short,
        [(byte)'L'] = PrintfFlags.LongDouble,
        [(byte)'q'] = PrintfFlags.LongLong,
        // size_t and curl_off_t are 64 bits on every platform Curl builds for.
        [(byte)'z'] = PrintfFlags.LongLong,
        [(byte)'O'] = PrintfFlags.LongLong,
    };

    private static readonly Dictionary<byte, (PrintfFlags Flags, ConversionKind Kind)> Conversions = new()
    {
        [(byte)'S'] = (PrintfFlags.Alternate, ConversionKind.String),
        [(byte)'s'] = (PrintfFlags.None, ConversionKind.String),
        [(byte)'n'] = (PrintfFlags.None, ConversionKind.CountPointer),
        [(byte)'p'] = (PrintfFlags.None, ConversionKind.Pointer),
        [(byte)'d'] = (PrintfFlags.None, ConversionKind.Signed),
        [(byte)'i'] = (PrintfFlags.None, ConversionKind.Signed),
        [(byte)'u'] = (PrintfFlags.Unsigned, ConversionKind.Unsigned),
        [(byte)'o'] = (PrintfFlags.Octal | PrintfFlags.Unsigned, ConversionKind.Unsigned),
        [(byte)'x'] = (PrintfFlags.Hex | PrintfFlags.Unsigned, ConversionKind.Unsigned),
        [(byte)'X'] = (PrintfFlags.Hex | PrintfFlags.Upper | PrintfFlags.Unsigned, ConversionKind.Unsigned),
        [(byte)'c'] = (PrintfFlags.Character, ConversionKind.Character),
        [(byte)'f'] = (PrintfFlags.None, ConversionKind.Floating),
        [(byte)'F'] = (PrintfFlags.Upper, ConversionKind.Floating),
        [(byte)'e'] = (PrintfFlags.FloatE, ConversionKind.Floating),
        [(byte)'E'] = (PrintfFlags.FloatE | PrintfFlags.Upper, ConversionKind.Floating),
        [(byte)'g'] = (PrintfFlags.FloatG, ConversionKind.Floating),
        [(byte)'G'] = (PrintfFlags.FloatG | PrintfFlags.Upper, ConversionKind.Floating),
    };

    private static readonly Dictionary<byte, Func<PrintfFormatParser, FlagRead>> FlagReaders = new()
    {
        [(byte)'-'] = parser => parser.AddLeft(),
        [(byte)'l'] = parser => parser.AddLong(),
        [(byte)'.'] = parser => parser.ReadPrecision(),
        [(byte)'*'] = parser => parser.ReadWidthArgument(),
        [(byte)'I'] = parser => parser.ReadWindowsLength(),
        [(byte)'0'] = parser => parser.ReadWidth(),
        [(byte)'1'] = parser => parser.ReadWidth(),
        [(byte)'2'] = parser => parser.ReadWidth(),
        [(byte)'3'] = parser => parser.ReadWidth(),
        [(byte)'4'] = parser => parser.ReadWidth(),
        [(byte)'5'] = parser => parser.ReadWidth(),
        [(byte)'6'] = parser => parser.ReadWidth(),
        [(byte)'7'] = parser => parser.ReadWidth(),
        [(byte)'8'] = parser => parser.ReadWidth(),
        [(byte)'9'] = parser => parser.ReadWidth(),
    };

    private readonly byte[] format;
    private readonly PrintfPlatform platform;
    private readonly List<PrintfSegment> segments = [];
    // Per argument position: the type it is read as, and whether a conversion (or width or precision) claimed it.
    private readonly PrintfInputType[] inputTypes = new PrintfInputType[MaxParameters];
    private readonly bool[] used = new bool[MaxParameters];
    private int index;
    private int textStart;
    private int nextParameter;
    private int maxParameter = -1;
    private DollarUse dollarUse;
    private PrintfFlags flags;
    private int width;
    private int precision;

    private PrintfFormatParser(byte[] format, PrintfPlatform platform)
    {
        this.format = format;
        this.platform = platform;
    }

    // Whether the format uses "n$" positions; the first conversion decides, and mixing the two styles is refused.
    private enum DollarUse
    {
        Unknown,
        No,
        Yes,
    }

    // The outcome of reading one flag, width, precision or length character in a conversion.
    private enum FlagRead
    {
        Read,
        End,
        Invalid,
    }

    // The kind of value a conversion character reads; with the length flags it picks the PrintfInputType.
    private enum ConversionKind
    {
        String,
        Pointer,
        CountPointer,
        Character,
        Signed,
        Unsigned,
        Floating,
    }

    /// <summary>Parses <paramref name="format"/>, which holds no NUL.</summary>
    /// <param name="format">The format's bytes.</param>
    /// <param name="platform">The platform whose length modifiers apply.</param>
    /// <returns>The parsed format, or <see langword="null"/> when <c>parsefmt</c> refuses it.</returns>
    public static PrintfFormat? Parse(byte[] format, PrintfPlatform platform) =>
        new PrintfFormatParser(format, platform).Parse();

    private static bool IsDigit(byte value) => value is >= (byte)'0' and <= (byte)'9';

    private static PrintfInputType SizedType(
        PrintfFlags conversionFlags, PrintfInputType plain, PrintfInputType withLong, PrintfInputType withLongLong)
    {
        if ((conversionFlags & PrintfFlags.LongLong) != 0)
        {
            return withLongLong;
        }

        return (conversionFlags & PrintfFlags.Long) != 0 ? withLong : plain;
    }

    private static PrintfInputType InputTypeOf(ConversionKind kind, PrintfFlags conversionFlags) => kind switch
    {
        ConversionKind.String => PrintfInputType.String,
        ConversionKind.Pointer => PrintfInputType.Pointer,
        ConversionKind.CountPointer => PrintfInputType.CountPointer,
        ConversionKind.Character => PrintfInputType.Int,
        ConversionKind.Signed => SizedType(conversionFlags, PrintfInputType.Int, PrintfInputType.Long, PrintfInputType.LongLong),
        ConversionKind.Unsigned => SizedType(
            conversionFlags, PrintfInputType.IntUnsigned, PrintfInputType.LongUnsigned, PrintfInputType.LongLongUnsigned),
        _ => (conversionFlags & PrintfFlags.LongDouble) != 0 ? PrintfInputType.LongDouble : PrintfInputType.Double,
    };

    private byte At(int position) => position < format.Length ? format[position] : (byte)0;

    private PrintfFormat? Parse()
    {
        while (index < format.Length)
        {
            if (format[index] != (byte)'%')
            {
                index++;
            }
            else if (!ParsePercent())
            {
                return null;
            }
        }

        return AddTextSegment(format.Length - textStart) ? ReadInputTypes() : null;
    }

    private PrintfFormat? ReadInputTypes()
    {
        for (int parameter = 0; parameter <= maxParameter; parameter++)
        {
            if (!used[parameter])
            {
                return null;
            }
        }

        return new PrintfFormat(format, segments, inputTypes[..(maxParameter + 1)]);
    }

    private bool ParsePercent()
    {
        index++;
        int textLength = index - textStart - 1;
        if (At(index) == (byte)'%')
        {
            // The second '%' starts the next text segment, so "%%" prints one '%'.
            bool added = AddTextSegment(textLength);
            textStart = index;
            index++;
            return added;
        }

        int parameter = ReadParameterPosition();
        if (parameter == InvalidPosition || !ReadFlags())
        {
            return false;
        }

        if (!Conversions.TryGetValue(At(index), out (PrintfFlags Flags, ConversionKind Kind) conversion))
        {
            // An unknown conversion is disregarded and stays in the output as text.
            return true;
        }

        flags |= conversion.Flags;
        return AddConversion(InputTypeOf(conversion.Kind, flags), parameter, textLength);
    }

    private bool AddTextSegment(int textLength)
    {
        if (textLength == 0)
        {
            return true;
        }

        if (segments.Count >= MaxSegments)
        {
            return false;
        }

        segments.Add(new PrintfSegment(0, 0, PrintfFlags.TextOnly, 0, textStart, textLength));
        return true;
    }

    private bool AddConversion(PrintfInputType type, int parameter, int textLength)
    {
        if (!ClaimSizeArgument(PrintfFlags.WidthFromArgument, ref width, PrintfInputType.Width)
            || !ClaimSizeArgument(PrintfFlags.PrecisionFromArgument, ref precision, PrintfInputType.Precision))
        {
            return false;
        }

        int input = parameter == NoPosition ? nextParameter++ : parameter;
        if (!Claim(input, type) || segments.Count >= MaxSegments)
        {
            return false;
        }

        segments.Add(new PrintfSegment(width, precision, flags, input, textStart, textLength));
        index++;
        textStart = index;
        return true;
    }

    private bool ClaimSizeArgument(PrintfFlags fromArgument, ref int position, PrintfInputType type)
    {
        if ((flags & fromArgument) == 0)
        {
            return true;
        }

        if (position < 0)
        {
            position = nextParameter++;
        }
        else if (used[position])
        {
            return false;
        }

        return Claim(position, type);
    }

    private bool Claim(int parameter, PrintfInputType type)
    {
        if (parameter >= MaxParameters)
        {
            return false;
        }

        maxParameter = Math.Max(maxParameter, parameter);
        inputTypes[parameter] = type;
        used[parameter] = true;
        return true;
    }

    private int ReadParameterPosition()
    {
        if (dollarUse == DollarUse.No)
        {
            return NoPosition;
        }

        int position = ReadDollarPosition();
        if (position >= 0)
        {
            dollarUse = DollarUse.Yes;
            return position;
        }

        if (dollarUse == DollarUse.Yes)
        {
            return InvalidPosition;
        }

        dollarUse = DollarUse.No;
        return NoPosition;
    }

    // dollarstring: "<n>$" with n from 1 to 128, answered zero-based; nothing is read when it is not one.
    private int ReadDollarPosition()
    {
        int cursor = index;
        if (!TryReadNumber(ref cursor, MaxParameters, out long number) || At(cursor) != (byte)'$' || number == 0)
        {
            return NoPosition;
        }

        index = cursor + 1;
        return (int)number - 1;
    }

    // curlx_str_number: one or more digits up to maximum; the cursor moves only when it answers true.
    private bool TryReadNumber(ref int cursor, long maximum, out long number)
    {
        number = 0;
        int position = cursor;
        if (!IsDigit(At(position)))
        {
            return false;
        }

        do
        {
            int digit = At(position++) - '0';
            if (number > (maximum - digit) / 10)
            {
                return false;
            }

            number = (number * 10) + digit;
        }
        while (IsDigit(At(position)));

        cursor = position;
        return true;
    }

    private bool ReadFlags()
    {
        flags = PrintfFlags.None;
        width = 0;
        precision = 0;
        while (true)
        {
            byte character = At(index++);
            FlagRead read = FlagCharacters.TryGetValue(character, out PrintfFlags flag) ? AddFlag(flag) : ReadFlag(character);
            if (read != FlagRead.Read)
            {
                index--;
                return read == FlagRead.End;
            }
        }
    }

    private FlagRead AddFlag(PrintfFlags flag)
    {
        flags |= flag;
        return FlagRead.Read;
    }

    private FlagRead ReadFlag(byte character) =>
        FlagReaders.TryGetValue(character, out Func<PrintfFormatParser, FlagRead>? reader) ? reader(this) : FlagRead.End;

    private FlagRead AddLeft()
    {
        flags = (flags | PrintfFlags.Left) & ~PrintfFlags.PadWithZeros;
        return FlagRead.Read;
    }

    private FlagRead AddLong() => AddFlag((flags & PrintfFlags.Long) != 0 ? PrintfFlags.LongLong : PrintfFlags.Long);

    // The digit that starts the width was read already: a leading '0' is the zero-padding flag.
    private FlagRead ReadWidth()
    {
        if (At(index - 1) == (byte)'0' && (flags & PrintfFlags.Left) == 0)
        {
            flags |= PrintfFlags.PadWithZeros;
        }

        flags |= PrintfFlags.Width;
        index--;
        if (!TryReadNumber(ref index, int.MaxValue, out long number))
        {
            return FlagRead.Invalid;
        }

        width = (int)number;
        return FlagRead.Read;
    }

    private FlagRead ReadWidthArgument()
    {
        flags |= PrintfFlags.WidthFromArgument;
        width = dollarUse == DollarUse.Yes ? ReadDollarPosition() : NoPosition;
        return dollarUse == DollarUse.Yes && width < 0 ? FlagRead.Invalid : FlagRead.Read;
    }

    private FlagRead ReadPrecision()
    {
        bool read = At(index) == (byte)'*' ? ReadPrecisionArgument() : ReadPrecisionNumber();
        const PrintfFlags both = PrintfFlags.Precision | PrintfFlags.PrecisionFromArgument;
        return read && (flags & both) != both ? FlagRead.Read : FlagRead.Invalid;
    }

    private bool ReadPrecisionArgument()
    {
        flags |= PrintfFlags.PrecisionFromArgument;
        index++;
        precision = dollarUse == DollarUse.Yes ? ReadDollarPosition() : NoPosition;
        return dollarUse != DollarUse.Yes || precision >= 0;
    }

    private bool ReadPrecisionNumber()
    {
        flags |= PrintfFlags.Precision;
        bool negative = At(index) == (byte)'-';
        if (negative)
        {
            index++;
        }

        if (!TryReadNumber(ref index, int.MaxValue, out long number))
        {
            return false;
        }

        precision = negative ? -(int)number : (int)number;
        return true;
    }

    // Windows' I32, I64 and bare I length modifiers; elsewhere 'I' ends the flags like any other letter.
    private FlagRead ReadWindowsLength()
    {
        if (!platform.IsWindows)
        {
            return FlagRead.End;
        }

        if (At(index) == (byte)'3' && At(index + 1) == (byte)'2')
        {
            index += 2;
            return AddFlag(PrintfFlags.Long);
        }

        if (At(index) == (byte)'6' && At(index + 1) == (byte)'4')
        {
            index += 2;
        }

        return AddFlag(PrintfFlags.LongLong);
    }
}
