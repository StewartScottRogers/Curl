using Curl.Protocol.Abstractions;
using Curl.Protocol.Ldap.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Adversarial black-box attacks on the LDAP library (BL-1512), by the method in
/// Documentation/Wiki/Adversarial-Testing.md: the frame reader, the BER response decoders,
/// the filter encoder and both URL readers at their boundaries, with malformed and fuzzed
/// input, in their invalid partitions, and under repeated calls. The oracle is each type's
/// documented contract: a refusal it names, never an exception.
/// </summary>
[TestClass]
public sealed class LdapAdversarialTests
{
    private const int FuzzSeed = 1512;

    private const int FuzzIterations = 2000;

    private static readonly byte[] SearchEntry = Hex.Bytes(
        "30 1d 02 01 02 64 18 04 04 63 6e 3d 61 30 10 30 0e 04 02 63 6e 31 08 04 01 61 04 03 62 63 64");

    private static readonly byte[] BindSuccess = Hex.Bytes("30 0c 02 01 01 61 07 0a 01 00 04 00 04 00");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("30 84 7f ff ff c1", 0x7FFFFFC7)]
    [DataRow("30 84 7f ff ff c2", LdapMessageReader.MalformedFrame)]
    [DataRow("30 84 80 00 00 00", LdapMessageReader.MalformedFrame)]
    [DataRow("30 81 7f", 130)]
    [DataRow("30 81 80", 131)]
    [DataRow("30 84 00 00 00 00", 6)]
    [DataRow("30 81", LdapMessageReader.NeedMoreBytes)]
    [DataRow("30 ff", LdapMessageReader.MalformedFrame)]
    public void MeasureFrame_LengthAtAndPastArrayMaxLengthAndNonMinimalLongForm_IsTheLengthOrMalformed(string bytes, int expected)
    {
        Diagnostics.Bytes("frame prefix", Hex.Bytes(bytes));

        int actual = LdapMessageReader.MeasureFrame(Hex.Bytes(bytes));

        Diagnostics.Assert("frame length", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void MeasureFrame_SeededRandomPrefixes_NeverThrowsAndNeverClaimsMoreThanArrayMaxLength()
    {
        var random = new Random(FuzzSeed);
        Diagnostics.Arrange("seed", FuzzSeed);
        for (int iteration = 0; iteration < FuzzIterations; iteration++)
        {
            byte[] prefix = new byte[random.Next(0, 8)];
            random.NextBytes(prefix);
            if (prefix.Length > 0 && random.Next(2) == 0)
            {
                prefix[0] = 0x30;
            }

            int length = LdapMessageReader.MeasureFrame(prefix);

            Assert.IsTrue(
                length is LdapMessageReader.NeedMoreBytes or LdapMessageReader.MalformedFrame || (length >= 2 && length <= Array.MaxLength),
                $"iteration {iteration}: {Convert.ToHexString(prefix)} measured {length}");
        }
    }

    [TestMethod]
    public async Task ReadMessageAsync_LongFormFrameSplitAtEveryOffset_ReturnsTheWholeMessage()
    {
        byte[] message = LongFormMessage(200);
        for (int split = 1; split < message.Length; split++)
        {
            var reader = new LdapMessageReader(new ScriptedConnection(message[..split], message[split..]));

            (LdapReadStatus status, byte[] read) = await reader.ReadMessageAsync(CancellationToken.None);

            Assert.AreEqual(LdapReadStatus.Message, status, $"split at {split}");
            CollectionAssert.AreEqual(message, read, $"split at {split}");
        }
    }

    [TestMethod]
    public async Task ReadMessageAsync_FrameLargerThanTheFirstBuffer_GrowsAndReturnsIt()
    {
        byte[] message = LongFormMessage(10_000);
        Diagnostics.Arrange("message length", message.Length);
        var reader = new LdapMessageReader(new ScriptedConnection(message));

        (LdapReadStatus status, byte[] read) = await reader.ReadMessageAsync(CancellationToken.None);

        Assert.AreEqual(LdapReadStatus.Message, status);
        CollectionAssert.AreEqual(message, read);
    }

    [TestMethod]
    public async Task ReadMessageAsync_ConnectionClosesInsideAClaimedHugeFrame_IsClosedWithoutWaitingForTheClaim()
    {
        var reader = new LdapMessageReader(new ScriptedConnection(Hex.Bytes("30 84 7f ff ff c1 02 01 01")));

        (LdapReadStatus status, byte[] read) = await reader.ReadMessageAsync(CancellationToken.None);

        Diagnostics.Assert("status", LdapReadStatus.Closed, status);
        Assert.AreEqual(LdapReadStatus.Closed, status);
        Assert.IsEmpty(read);
    }

    [TestMethod]
    public async Task ReadMessageAsync_CalledAgainAfterClosed_StaysClosed()
    {
        var reader = new LdapMessageReader(new ScriptedConnection(BindSuccess[..5]));

        (LdapReadStatus first, _) = await reader.ReadMessageAsync(CancellationToken.None);
        (LdapReadStatus second, _) = await reader.ReadMessageAsync(CancellationToken.None);

        Assert.AreEqual(LdapReadStatus.Closed, first);
        Assert.AreEqual(LdapReadStatus.Closed, second);
    }

    [TestMethod]
    public async Task ReadMessageAsync_CalledAgainAfterMalformed_StaysMalformed()
    {
        var reader = new LdapMessageReader(new ScriptedConnection([.. BindSuccess, 0x04, 0x00]));

        (LdapReadStatus first, _) = await reader.ReadMessageAsync(CancellationToken.None);
        (LdapReadStatus second, _) = await reader.ReadMessageAsync(CancellationToken.None);
        (LdapReadStatus third, _) = await reader.ReadMessageAsync(CancellationToken.None);

        Assert.AreEqual(LdapReadStatus.Message, first);
        Assert.AreEqual(LdapReadStatus.Malformed, second);
        Assert.AreEqual(LdapReadStatus.Malformed, third);
    }

    [TestMethod]
    public async Task ReadMessageAsync_ManyMessagesInOneRead_ReturnsEachInOrder()
    {
        byte[] reads = [.. Enumerable.Repeat(BindSuccess, 100).SelectMany(message => message)];
        var reader = new LdapMessageReader(new ScriptedConnection(reads));

        for (int index = 0; index < 100; index++)
        {
            (LdapReadStatus status, byte[] read) = await reader.ReadMessageAsync(CancellationToken.None);
            Assert.AreEqual(LdapReadStatus.Message, status, $"message {index}");
            CollectionAssert.AreEqual(BindSuccess, read, $"message {index}");
        }

        (LdapReadStatus last, _) = await reader.ReadMessageAsync(CancellationToken.None);
        Assert.AreEqual(LdapReadStatus.Closed, last);
    }

    [TestMethod]
    [DataRow("7f ff ff ff", true)]
    [DataRow("00 80 00 00 00", false)]
    [DataRow("80", false)]
    [DataRow("ff ff ff ff ff ff ff ff ff", false)]
    public void SearchDecode_ResultCodeAtAndPastIntMaxValueOrNegative_IsDoneOrLost(string resultCode, bool isDone)
    {
        byte[] message = SearchDone(resultCode);
        Diagnostics.Bytes("message", message);

        LdapSearchReply reply = LdapSearchResponse.Decode(message, 2);

        Diagnostics.Act("reply", reply);
        Assert.AreEqual(isDone ? LdapSearchReplyKind.Done : LdapSearchReplyKind.Lost, reply.Kind);
    }

    [TestMethod]
    [DataRow("7f ff ff ff", true)]
    [DataRow("00 80 00 00 00", false)]
    [DataRow("80", false)]
    public void BindDecode_ResultCodeAtAndPastIntMaxValueOrNegative_IsAnsweredOrMalformed(string resultCode, bool isAnswered)
    {
        byte[] content = [.. Hex.Bytes("02 01 01"), .. Tlv(0x61, [.. Tlv(0x0a, Hex.Bytes(resultCode)), 0x04, 0x00, 0x04, 0x00])];
        byte[] message = Tlv(0x30, content);
        Diagnostics.Bytes("message", message);

        LdapBindReply reply = LdapBindResponse.Decode(message, 1);

        Diagnostics.Act("reply", reply);
        Assert.AreEqual(isAnswered ? LdapBindReplyStatus.Answered : LdapBindReplyStatus.Malformed, reply.Status);
    }

    [TestMethod]
    public void SearchDecode_MessageIdPastIntMaxValue_IsLost()
    {
        byte[] message = Tlv(0x30, [.. Hex.Bytes("02 05 00 80 00 00 00"), .. Tlv(0x65, Hex.Bytes("0a 01 00 04 00 04 00"))]);

        LdapSearchReply reply = LdapSearchResponse.Decode(message, 2);

        Assert.AreEqual(LdapSearchReplyKind.Lost, reply.Kind);
    }

    [TestMethod]
    public void SearchDecode_EntryWithIndefiniteLengthAttributeList_IsTheEntry()
    {
        byte[] message = Hex.Bytes(
            "30 1f 02 01 02 64 1a 04 04 63 6e 3d 61 30 80 30 0e 04 02 63 6e 31 08 04 01 61 04 03 62 63 64 00 00");

        LdapSearchReply reply = LdapSearchResponse.Decode(message, 2);

        Diagnostics.Act("reply", reply);
        Assert.AreEqual(LdapSearchReplyKind.Entry, reply.Kind);
        Assert.HasCount(1, reply.Entry!.Attributes!);
        Assert.HasCount(2, reply.Entry.Attributes![0].Values);
    }

    [TestMethod]
    public void SearchDecode_TrailingBytesAfterTheMessage_IsLost()
    {
        byte[] message = [.. SearchEntry, 0x00];

        LdapSearchReply reply = LdapSearchResponse.Decode(message, 2);

        Assert.AreEqual(LdapSearchReplyKind.Lost, reply.Kind);
    }

    [TestMethod]
    public void SearchDecode_EveryTruncation_NeverThrows()
    {
        for (int length = 0; length < SearchEntry.Length; length++)
        {
            LdapSearchReply reply = LdapSearchResponse.Decode(SearchEntry[..length], 2);

            Assert.AreEqual(LdapSearchReplyKind.Lost, reply.Kind, $"truncated to {length}");
        }
    }

    [TestMethod]
    public void BindDecode_EveryTruncation_IsMalformed()
    {
        for (int length = 0; length < BindSuccess.Length; length++)
        {
            LdapBindReply reply = LdapBindResponse.Decode(BindSuccess[..length], 1);

            Assert.AreEqual(LdapBindReplyStatus.Malformed, reply.Status, $"truncated to {length}");
        }
    }

    [TestMethod]
    public void SearchDecode_SeededRandomByteMutations_NeverThrows()
    {
        var random = new Random(FuzzSeed);
        Diagnostics.Arrange("seed", FuzzSeed);
        for (int iteration = 0; iteration < FuzzIterations; iteration++)
        {
            byte[] mutant = Mutate(random, SearchEntry);

            try
            {
                _ = LdapSearchResponse.Decode(mutant, 2);
            }
            catch (Exception exception)
            {
                Assert.Fail($"seed {FuzzSeed}, iteration {iteration}: {Convert.ToHexString(mutant)} threw {exception.GetType().Name}");
            }
        }
    }

    [TestMethod]
    public void BindDecode_SeededRandomByteMutations_NeverThrows()
    {
        var random = new Random(FuzzSeed);
        Diagnostics.Arrange("seed", FuzzSeed);
        for (int iteration = 0; iteration < FuzzIterations; iteration++)
        {
            byte[] mutant = Mutate(random, BindSuccess);

            try
            {
                _ = LdapBindResponse.Decode(mutant, 1);
            }
            catch (Exception exception)
            {
                Assert.Fail($"seed {FuzzSeed}, iteration {iteration}: {Convert.ToHexString(mutant)} threw {exception.GetType().Name}");
            }
        }
    }

    [TestMethod]
    public void SearchDecode_SameMessageOnManyTasksAtOnce_GivesTheSameReplyAsOneCall()
    {
        LdapSearchReply expected = LdapSearchResponse.Decode(SearchEntry, 2);

        LdapSearchReply[] replies = new LdapSearchReply[64];
        Parallel.For(0, replies.Length, index => replies[index] = LdapSearchResponse.Decode(SearchEntry, 2));

        foreach (LdapSearchReply reply in replies)
        {
            Assert.AreEqual(expected.Kind, reply.Kind);
            CollectionAssert.AreEqual(expected.Entry!.Dn, reply.Entry!.Dn);
        }
    }

    [TestMethod]
    [DataRow("cn=a)")]
    [DataRow("((cn=a)")]
    [DataRow("(c n=a)")]
    public void Encode_ParenthesisOrBlankInsideTheAttribute_IsRefusedByOpenLdap(string filter)
    {
        Diagnostics.Arrange("filter", filter);

        byte[]? open = Encoder(LdapDialect.OpenLdap).Encode(filter);

        Assert.IsNull(open);
    }

    [TestMethod]
    [DataRow("(cn=a")]
    [DataRow("(&(cn=a)")]
    [DataRow("(cn=\\")]
    [DataRow("(=a)")]
    [DataRow("()")]
    [DataRow("(")]
    [DataRow(")")]
    [DataRow("")]
    [DataRow("(!)")]
    [DataRow("(cn=a))")]
    [DataRow("(cn~a)")]
    public void Encode_UnbalancedOrEmptyFilter_IsRefusedInBothDialects(string filter)
    {
        Diagnostics.Arrange("filter", filter);

        byte[]? open = Encoder(LdapDialect.OpenLdap).Encode(filter);
        byte[]? win = Encoder(LdapDialect.WinLdap).Encode(filter);

        Assert.IsNull(open, "OpenLDAP");
        Assert.IsNull(win, "WinLDAP");
    }

    [TestMethod]
    public void Encode_FilterNestedTwoHundredDeep_EncodesOneConstructedPerLevel()
    {
        const int depth = 200;
        string filter = string.Concat(Enumerable.Repeat("(&", depth)) + "(cn=a)" + new string(')', depth);

        byte[]? encoded = Encoder(LdapDialect.OpenLdap).Encode(filter);

        Assert.IsNotNull(encoded);
        Assert.AreEqual(0xA0, encoded[0]);
    }

    [TestMethod]
    public void Encode_ReusedAfterARefusal_EncodesTheNextFilterAsAFreshEncoderDoes()
    {
        LdapFilterEncoder encoder = Encoder(LdapDialect.OpenLdap);

        Assert.IsNull(encoder.Encode("(&(cn=a)"));
        byte[]? reused = encoder.Encode("(|(cn=a)(sn=b))");

        CollectionAssert.AreEqual(Encoder(LdapDialect.OpenLdap).Encode("(|(cn=a)(sn=b))"), reused);
    }

    [TestMethod]
    public void Encode_SeededRandomFilters_NeverThrowInEitherDialect()
    {
        const string alphabet = "()&|!=*~<>:\\ cnaZ09%,";
        var random = new Random(FuzzSeed);
        Diagnostics.Arrange("seed", FuzzSeed);
        for (int iteration = 0; iteration < FuzzIterations; iteration++)
        {
            string filter = new([.. Enumerable.Range(0, random.Next(0, 24)).Select(_ => alphabet[random.Next(alphabet.Length)])]);

            foreach (LdapDialect dialect in new[] { LdapDialect.OpenLdap, LdapDialect.WinLdap })
            {
                try
                {
                    _ = Encoder(dialect).Encode(filter);
                }
                catch (Exception exception)
                {
                    Assert.Fail($"seed {FuzzSeed}, iteration {iteration}, {dialect}: \"{filter}\" threw {exception.GetType().Name}");
                }
            }
        }
    }

    [TestMethod]
    [DataRow("dc=x?cn?bogus")]
    [DataRow("dc=x?cn?sub?%4")]
    [DataRow("dc=x?cn?sub?%zz")]
    [DataRow("dc=x?cn?sub?(cn=a)?")]
    [DataRow("dc=x?cn?sub?(cn=a)?x?y")]
    [DataRow("dc=x?%00?sub")]
    [DataRow("%00?cn?sub?(cn=a)")]
    [DataRow("dc=x????")]
    [DataRow("dc=x?" + "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void Read_InvalidQueryPartitions_IsASearchOrARefusalWithExit3InBothBuilds(string path)
    {
        CurlUrl url = CurlUrl.Parse("ldap://h/" + path);
        Diagnostics.Arrange("path", path);

        AssertSearchOrUrlMalformat(OpenLdapUrlReader.Read(url), "OpenLDAP");
        AssertSearchOrUrlMalformat(WinLdapUrlReader.Read(url), "WinLDAP");
    }

    [TestMethod]
    public void Read_SeededRandomQueries_NeverThrowInEitherBuild()
    {
        const string alphabet = "?%,()=*\\:/aZ09 ";
        var random = new Random(FuzzSeed);
        Diagnostics.Arrange("seed", FuzzSeed);
        for (int iteration = 0; iteration < FuzzIterations; iteration++)
        {
            string query = new([.. Enumerable.Range(0, random.Next(0, 20)).Select(_ => alphabet[random.Next(alphabet.Length)])]);
            if (!CurlUrl.TryParse("ldap://h/dc=x?" + query, false, out CurlUrl? url))
            {
                continue;
            }

            try
            {
                AssertSearchOrUrlMalformat(OpenLdapUrlReader.Read(url), "OpenLDAP");
                AssertSearchOrUrlMalformat(WinLdapUrlReader.Read(url), "WinLDAP");
            }
            catch (Exception exception) when (exception is not AssertFailedException)
            {
                Assert.Fail($"seed {FuzzSeed}, iteration {iteration}: \"{query}\" threw {exception.GetType().Name}");
            }
        }
    }

    [TestMethod]
    [DataRow("%", 0)]
    [DataRow("%4", 0)]
    [DataRow("%41", 0)]
    [DataRow("a%4", 1)]
    [DataRow("%g1", 0)]
    [DataRow("%1g", 0)]
    public void TryRead_EscapeCutOffOrNotHex_ReadsOnlyAWholeEscape(string text, int index)
    {
        bool read = LdapHexEscape.TryRead(text, index, '%', out char octet);

        bool whole = text.Length - index == 3 && Uri.IsHexDigit(text[index + 1]) && Uri.IsHexDigit(text[index + 2]);
        Assert.AreEqual(whole, read);
        Assert.AreEqual(whole ? 'A' : '\0', octet);
    }

    private static void AssertSearchOrUrlMalformat(LdapUrlReading reading, string build)
    {
        Assert.IsTrue(reading.Search is not null ^ reading.Failure is not null, $"{build}: exactly one of search and failure");
        if (reading.Failure is TransferResult failure)
        {
            Assert.AreEqual(CurlExitCode.UrlMalformat, failure.ExitCode, build);
        }
    }

    private static LdapFilterEncoder Encoder(LdapDialect dialect) => new(dialect, new LdapBerWriter(dialect));

    private static byte[] SearchDone(string resultCodeHex) =>
        Tlv(0x30, [.. Hex.Bytes("02 01 02"), .. Tlv(0x65, [.. Tlv(0x0a, Hex.Bytes(resultCodeHex)), 0x04, 0x00, 0x04, 0x00])]);

    private static byte[] LongFormMessage(int padding) =>
        Tlv(0x30, [.. Hex.Bytes("02 01 01"), .. Tlv(0x04, new byte[padding])]);

    private static byte[] Tlv(byte tag, byte[] content)
    {
        byte[] length = content.Length < 0x80 ? [(byte)content.Length]
            : content.Length < 0x100 ? [0x81, (byte)content.Length]
            : [0x82, (byte)(content.Length >> 8), (byte)content.Length];
        return [tag, .. length, .. content];
    }

    private static byte[] Mutate(Random random, byte[] original)
    {
        byte[] mutant = [.. original];
        int changes = random.Next(1, 4);
        for (int change = 0; change < changes; change++)
        {
            mutant[random.Next(mutant.Length)] = (byte)random.Next(256);
        }

        return mutant;
    }
}
