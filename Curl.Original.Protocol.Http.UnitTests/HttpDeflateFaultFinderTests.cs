namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpDeflateFaultFinder" /> against zlib's inflate (RFC 1951, zlib's
/// inflate.c and inftrees.c): each stream below is built bit by bit and must give the text zlib
/// gives for its first fault, or none when it ends cleanly or runs out first (STD-208, BL-6018).
/// </summary>
[TestClass]
public sealed class HttpDeflateFaultFinderTests
{
    private const int EndOfBlock = 256;

    private const int FirstLength = 257;

    private static readonly int[] CodeLengthOrder = [16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15];

    [TestMethod]
    public void Find_NoBytes_FindsNoFault()
    {
        string? fault = HttpDeflateFaultFinder.Find([]);

        Assert.IsNull(fault);
    }

    [TestMethod]
    public void Find_ReservedBlockType_IsInvalidBlockType()
    {
        byte[] stream = new DeflateBitWriter().Bits(1, 1).Bits(3, 2).ToArray();

        string? fault = HttpDeflateFaultFinder.Find(stream);

        Assert.AreEqual(HttpTransferMessages.InvalidBlockType, fault);
    }

    [TestMethod]
    public void Find_StoredBlockThenCopyWithinIt_FindsNoFault()
    {
        DeflateBitWriter writer = new DeflateBitWriter().Bits(0, 1).Bits(0, 2).Align().Bits(3, 16).Bits(0xFFFC, 16).Bits('x', 8).Bits('y', 8).Bits('z', 8);
        writer.Bits(1, 1).Bits(1, 2).Fixed(FirstLength).Code(2, 5).Fixed(EndOfBlock);

        string? fault = HttpDeflateFaultFinder.Find(writer.ToArray());

        Assert.IsNull(fault);
    }

    [TestMethod]
    public void Find_StoredLengthsDisagree_IsInvalidStoredBlockLengths()
    {
        byte[] stream = new DeflateBitWriter().Bits(1, 1).Bits(0, 2).Align().Bits(3, 16).Bits(0, 16).ToArray();

        string? fault = HttpDeflateFaultFinder.Find(stream);

        Assert.AreEqual(HttpTransferMessages.InvalidStoredBlockLengths, fault);
    }

    [TestMethod]
    public void Find_StoredBlockLongerThanTheBytes_FindsNoFault()
    {
        byte[] stream = new DeflateBitWriter().Bits(0, 1).Bits(0, 2).Align().Bits(10, 16).Bits(0xFFF5, 16).Bits('x', 8).ToArray();

        string? fault = HttpDeflateFaultFinder.Find(stream);

        Assert.IsNull(fault);
    }

    [TestMethod]
    public void Find_FixedBlockEndingBeforeItsEnd_FindsNoFault()
    {
        byte[] stream = new DeflateBitWriter().Bits(1, 1).Bits(1, 2).Fixed('a').ToArray();

        string? fault = HttpDeflateFaultFinder.Find(stream);

        Assert.IsNull(fault);
    }

    [TestMethod]
    public void Find_CopyFromFurtherBackThanDecoded_IsInvalidDistanceTooFarBack()
    {
        byte[] stream = TheThenCopyFromFiveBack();

        string? fault = HttpDeflateFaultFinder.Find(stream);

        Assert.AreEqual(HttpTransferMessages.InvalidDistanceTooFarBack, fault);
    }

    [TestMethod]
    public void Find_FixedLengthSymbol286_IsInvalidLiteralLengthCode()
    {
        byte[] stream = new DeflateBitWriter().Bits(1, 1).Bits(1, 2).Fixed('a').Fixed(286).ToArray();

        string? fault = HttpDeflateFaultFinder.Find(stream);

        Assert.AreEqual(HttpTransferMessages.InvalidLiteralLengthCode, fault);
    }

    [TestMethod]
    public void Find_FixedDistanceCode30_IsInvalidDistanceCode()
    {
        byte[] stream = new DeflateBitWriter().Bits(1, 1).Bits(1, 2).Fixed('a').Fixed(FirstLength).Code(30, 5).ToArray();

        string? fault = HttpDeflateFaultFinder.Find(stream);

        Assert.AreEqual(HttpTransferMessages.InvalidDistanceCode, fault);
    }

    [TestMethod]
    public void Find_DynamicBlockWithTooManyLengthSymbols_IsTooManyLengthOrDistanceSymbols()
    {
        byte[] stream = new DeflateBitWriter().Bits(1, 1).Bits(2, 2).Bits(30, 5).Bits(0, 5).Bits(0, 4).ToArray();

        string? fault = HttpDeflateFaultFinder.Find(stream);

        Assert.AreEqual(HttpTransferMessages.TooManyLengthOrDistanceSymbols, fault);
    }

    [TestMethod]
    public void Find_DynamicBlockWithTooManyDistanceSymbols_IsTooManyLengthOrDistanceSymbols()
    {
        byte[] stream = new DeflateBitWriter().Bits(1, 1).Bits(2, 2).Bits(0, 5).Bits(30, 5).Bits(0, 4).ToArray();

        string? fault = HttpDeflateFaultFinder.Find(stream);

        Assert.AreEqual(HttpTransferMessages.TooManyLengthOrDistanceSymbols, fault);
    }

    [TestMethod]
    [DataRow(new[] { 16, 17, 18 }, DisplayName = "over-subscribed")]
    [DataRow(new[] { 0 }, DisplayName = "incomplete, one code one bit long")]
    public void Find_RefusedCodeOfCodeLengths_IsInvalidCodeLengthsSet(int[] oneBitSymbols)
    {
        int[] codeLengthLengths = new int[19];
        foreach (int symbol in oneBitSymbols)
        {
            codeLengthLengths[symbol] = 1;
        }

        byte[] stream = DynamicHeader(new DeflateBitWriter(), 257, 1, codeLengthLengths).ToArray();

        string? fault = HttpDeflateFaultFinder.Find(stream);

        Assert.AreEqual(HttpTransferMessages.InvalidCodeLengthsSet, fault);
    }

    [TestMethod]
    public void Find_EmptyCodeOfCodeLengths_IsMissingEndOfBlock()
    {
        byte[] stream = DynamicHeader(new DeflateBitWriter(), 257, 1, new int[19]).ToArray();

        string? fault = HttpDeflateFaultFinder.Find(stream);

        Assert.AreEqual(HttpTransferMessages.MissingEndOfBlock, fault);
    }

    [TestMethod]
    public void Find_RepeatBeforeTheFirstLength_IsInvalidBitLengthRepeat()
    {
        int[] codeLengthLengths = new int[19];
        codeLengthLengths[0] = codeLengthLengths[16] = 1;
        DeflateBitWriter writer = DynamicHeader(new DeflateBitWriter(), 257, 1, codeLengthLengths);

        string? fault = HttpDeflateFaultFinder.Find(writer.Code(1, 1).Bits(0, 2).ToArray());

        Assert.AreEqual(HttpTransferMessages.InvalidBitLengthRepeat, fault);
    }

    [TestMethod]
    public void Find_RepeatsPastTheSymbolCounts_IsInvalidBitLengthRepeat()
    {
        int[] codeLengthLengths = new int[19];
        codeLengthLengths[1] = codeLengthLengths[16] = codeLengthLengths[17] = codeLengthLengths[18] = 2;
        DeflateBitWriter writer = DynamicHeader(new DeflateBitWriter(), 257, 1, codeLengthLengths);
        writer.Code(0, 2).Code(1, 2).Bits(0, 2).Code(2, 2).Bits(0, 3).Code(3, 2).Bits(127, 7).Code(3, 2).Bits(127, 7);

        string? fault = HttpDeflateFaultFinder.Find(writer.ToArray());

        Assert.AreEqual(HttpTransferMessages.InvalidBitLengthRepeat, fault);
    }

    [TestMethod]
    public void Find_NoCodeForEndOfBlock_IsMissingEndOfBlock()
    {
        string? fault = HttpDeflateFaultFinder.Find(Dynamic(Lengths(258, (97, 1), (257, 1)), [1]).ToArray());

        Assert.AreEqual(HttpTransferMessages.MissingEndOfBlock, fault);
    }

    [TestMethod]
    [DataRow(1, DisplayName = "over-subscribed")]
    [DataRow(2, DisplayName = "incomplete, longest code two bits")]
    public void Find_RefusedLiteralLengthCode_IsInvalidLiteralLengthsSet(int endOfBlockLength)
    {
        int[] literals = Lengths(258, (97, 1), (EndOfBlock, endOfBlockLength), (98, endOfBlockLength == 1 ? 1 : 0));

        string? fault = HttpDeflateFaultFinder.Find(Dynamic(literals, [1]).ToArray());

        Assert.AreEqual(HttpTransferMessages.InvalidLiteralLengthsSet, fault);
    }

    [TestMethod]
    public void Find_OverSubscribedDistanceCode_IsInvalidDistancesSet()
    {
        string? fault = HttpDeflateFaultFinder.Find(Dynamic(ValidLiterals(), [1, 1, 1]).ToArray());

        Assert.AreEqual(HttpTransferMessages.InvalidDistancesSet, fault);
    }

    [TestMethod]
    public void Find_DynamicBlockCopyingWithinWhatItDecoded_FindsNoFault()
    {
        DeflateBitWriter writer = Dynamic(ValidLiterals(), [1]).Code(0, 1).Code(3, 2).Code(0, 1).Code(2, 2);

        string? fault = HttpDeflateFaultFinder.Find(writer.ToArray());

        Assert.IsNull(fault);
    }

    [TestMethod]
    [DataRow(new[] { 1 }, DisplayName = "one-bit distance code, code 1")]
    [DataRow(new[] { 0 }, DisplayName = "no distance codes")]
    public void Find_DistanceCodeNoSymbolHas_IsInvalidDistanceCode(int[] distances)
    {
        DeflateBitWriter writer = Dynamic(ValidLiterals(), distances).Code(0, 1).Code(3, 2).Code(1, 1);

        string? fault = HttpDeflateFaultFinder.Find(writer.ToArray());

        Assert.AreEqual(HttpTransferMessages.InvalidDistanceCode, fault);
    }

    [TestMethod]
    public void Find_LiteralCodeNoSymbolHas_IsInvalidLiteralLengthCode()
    {
        DeflateBitWriter writer = Dynamic(Lengths(258, (EndOfBlock, 1)), [1]).Code(1, 1);

        string? fault = HttpDeflateFaultFinder.Find(writer.ToArray());

        Assert.AreEqual(HttpTransferMessages.InvalidLiteralLengthCode, fault);
    }

    /// <summary>
    /// A fixed block, not the last, that decodes <c>The </c> and then copies from 5 bytes back:
    /// corrupt deflate as curl names it, <c>invalid distance too far back</c>.
    /// </summary>
    internal static byte[] TheThenCopyFromFiveBack() =>
        new DeflateBitWriter().Bits(0, 1).Bits(1, 2).Fixed('T').Fixed('h').Fixed('e').Fixed(' ')
            .Fixed(FirstLength).Code(4, 5).Bits(0, 1).Fixed(EndOfBlock).ToArray();

    /// <summary>Literal/length code lengths: <c>a</c> one bit, end of block and length 3 two bits.</summary>
    private static int[] ValidLiterals() => Lengths(258, (97, 1), (EndOfBlock, 2), (FirstLength, 2));

    private static int[] Lengths(int count, params (int Symbol, int Length)[] codes)
    {
        int[] lengths = new int[count];
        foreach ((int symbol, int length) in codes)
        {
            lengths[symbol] = length;
        }

        return lengths;
    }

    /// <summary>
    /// Starts a last dynamic block with these code lengths, sent with a code of code lengths
    /// giving 0, 1, 2 and 18 two bits each (codes equal to 0, 1, 2 and 3).
    /// </summary>
    private static DeflateBitWriter Dynamic(int[] literals, int[] distances)
    {
        int[] codeLengthLengths = new int[19];
        codeLengthLengths[0] = codeLengthLengths[1] = codeLengthLengths[2] = codeLengthLengths[18] = 2;
        DeflateBitWriter writer = DynamicHeader(new DeflateBitWriter(), literals.Length, distances.Length, codeLengthLengths);
        int[] lengths = [.. literals, .. distances];
        for (int at = 0; at < lengths.Length;)
        {
            int zeros = 0;
            while (at + zeros < lengths.Length && lengths[at + zeros] == 0 && zeros < 138)
            {
                zeros++;
            }

            if (zeros >= 11)
            {
                writer.Code(3, 2).Bits(zeros - 11, 7);
                at += zeros;
            }
            else
            {
                writer.Code(lengths[at], 2);
                at++;
            }
        }

        return writer;
    }

    private static DeflateBitWriter DynamicHeader(DeflateBitWriter writer, int literalCount, int distanceCount, int[] codeLengthLengths)
    {
        writer.Bits(1, 1).Bits(2, 2).Bits(literalCount - 257, 5).Bits(distanceCount - 1, 5).Bits(15, 4);
        foreach (int symbol in CodeLengthOrder)
        {
            writer.Bits(codeLengthLengths[symbol], 3);
        }

        return writer;
    }

    /// <summary>Writes a deflate stream bit by bit: values first bit lowest, codes first bit highest.</summary>
    internal sealed class DeflateBitWriter
    {
        private readonly List<byte> bytes = [];

        private int count;

        public DeflateBitWriter Bits(int value, int length)
        {
            for (int i = 0; i < length; i++, count++)
            {
                if (count == bytes.Count * 8)
                {
                    bytes.Add(0);
                }

                bytes[^1] |= (byte)(((value >> i) & 1) << (count & 7));
            }

            return this;
        }

        public DeflateBitWriter Code(int code, int length)
        {
            for (int i = length - 1; i >= 0; i--)
            {
                Bits((code >> i) & 1, 1);
            }

            return this;
        }

        /// <summary>Writes <paramref name="symbol" />'s code in the fixed literal/length code.</summary>
        public DeflateBitWriter Fixed(int symbol) => symbol switch
        {
            < 144 => Code(0x30 + symbol, 8),
            < 256 => Code(0x190 + symbol - 144, 9),
            < 280 => Code(symbol - 256, 7),
            _ => Code(0xC0 + symbol - 280, 8),
        };

        public DeflateBitWriter Align()
        {
            count = bytes.Count * 8;
            return this;
        }

        public byte[] ToArray() => [.. bytes];
    }
}
