using System.Net;

using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Attacks <c>Curl.Networking.UnitLibrary</c>'s public surface as a black box, by the method in
/// Documentation/Wiki/Adversarial-Testing.md (BL-1502): DNS answers and HTTPS record data at their
/// count and length limits, cut short at every offset, with compression-pointer loops and pointers
/// past the end, and as seeded random bytes; <c>--resolve</c> and <c>--connect-to</c> values at the
/// port limits and in every invalid partition, with the exit 49 text measured on curl 8.21.0
/// (Windows, Schannel); and the parsed overrides read from many threads at once. The oracle is
/// RFC 1035 and RFC 9460, curl 8.21.0's <c>lib/doh.c</c>, and the libraries' own documented contracts.
/// </summary>
[TestClass]
public sealed class NetworkingAdversarialTests
{
    /// <summary>The question for <c>example.test</c> type A, at offset 12.</summary>
    private const string QuestionA = "076578616D706C6504746573740000010001";

    /// <summary>An A record for the question's name (a pointer to offset 12), TTL 60, 127.0.0.1.</summary>
    private const string AnswerA = "C00C000100010000003C00047F000001";

    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    // Family 1, boundaries: a message one byte short of its 12-byte header, and nothing at all.
    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(11)]
    public void DnsAnswerDecode_MessageShorterThanTheHeader_IsTooSmall(int length)
    {
        var answer = DnsAnswerDecoder.Decode(new byte[length], DnsRecordType.A);

        Assert.AreEqual(DnsMessageFailure.TooSmall, answer.Failure);
    }

    // Family 1, boundaries: exactly the header, with every count zero.
    [TestMethod]
    public void DnsAnswerDecode_BareHeaderWithNoRecords_IsNoContent()
    {
        var answer = DnsAnswerDecoder.Decode(new byte[12], DnsRecordType.A);

        Assert.AreEqual(DnsMessageFailure.NoContent, answer.Failure);
        Assert.IsEmpty(answer.Addresses);
    }

    // Family 1, boundaries: curl's dohentry holds 24 addresses; one more is read and dropped.
    [TestMethod]
    [DataRow(23, 23)]
    [DataRow(24, 24)]
    [DataRow(25, 24)]
    public void DnsAnswerDecode_ARecordsAroundTheTwentyFourAddressLimit_KeepsAtMostTwentyFour(int records, int expected)
    {
        var answer = DnsAnswerDecoder.Decode(Message(Header(1, records) + QuestionA + Repeat(AnswerA, records)), DnsRecordType.A);
        TestDiagnostics.For(TestContext).Act("failure", answer.Failure);

        Assert.AreEqual(DnsMessageFailure.None, answer.Failure);
        Assert.HasCount(expected, answer.Addresses);
    }

    // Family 1, boundaries: curl's dohentry holds 4 CNAMEs; a fifth is read and dropped.
    [TestMethod]
    [DataRow(4, 4)]
    [DataRow(5, 4)]
    public void DnsAnswerDecode_CnameRecordsAroundTheFourNameLimit_KeepsAtMostFour(int records, int expected)
    {
        // A CNAME for the question's name whose target is the question's name.
        const string cname = "C00C000500010000003C0002C00C";

        var answer = DnsAnswerDecoder.Decode(Message(Header(1, records) + QuestionA + Repeat(cname, records)), DnsRecordType.A);

        Assert.AreEqual(DnsMessageFailure.None, answer.Failure);
        Assert.HasCount(expected, answer.CanonicalNames);
        Assert.AreEqual("example.test", answer.CanonicalNames[0]);
    }

    // Family 1, boundaries: doh_store_cname gives up after 128 labels and pointers.
    [TestMethod]
    [DataRow(127, DnsMessageFailure.None)]
    [DataRow(128, DnsMessageFailure.LabelLoop)]
    public void DnsAnswerDecode_CnameTargetAroundTheHundredAndTwentyEightLabelLimit_StopsAtTheLimit(int labels, DnsMessageFailure expected)
    {
        var target = Repeat("0161", labels) + "00";
        var cname = "C00C000500010000003C" + (target.Length / 2).ToString("X4") + target;

        var answer = DnsAnswerDecoder.Decode(Message(Header(1, 1) + QuestionA + cname), DnsRecordType.A);

        Assert.AreEqual(expected, answer.Failure);
    }

    // Family 1, boundaries: an A record's data one byte either side of 4, an AAAA's either side of 16.
    [TestMethod]
    [DataRow(DnsRecordType.A, 3)]
    [DataRow(DnsRecordType.A, 5)]
    [DataRow(DnsRecordType.Aaaa, 15)]
    [DataRow(DnsRecordType.Aaaa, 17)]
    public void DnsAnswerDecode_AddressDataOneByteOffItsSize_IsRdataLength(DnsRecordType type, int dataLength)
    {
        var question = "076578616D706C650474657374" + "00" + ((int)type).ToString("X4") + "0001";
        var record = "C00C" + ((int)type).ToString("X4") + "00010000003C" + dataLength.ToString("X4") + new string('0', dataLength * 2);

        var answer = DnsAnswerDecoder.Decode(Message(Header(1, 1) + question + record), type);

        Assert.AreEqual(DnsMessageFailure.RdataLength, answer.Failure);
    }

    // Family 1, boundaries: a TTL of 0 and of 2^32 - 1; the reported TTL never exceeds int.MaxValue.
    [TestMethod]
    [DataRow("00000000", 0u)]
    [DataRow("7FFFFFFF", (uint)int.MaxValue)]
    [DataRow("FFFFFFFF", (uint)int.MaxValue)]
    public void DnsAnswerDecode_TtlAtItsExtremes_ReportsTheSmallerOfItAndIntMaxValue(string ttl, uint expected)
    {
        var record = "C00C00010001" + ttl + "00047F000001";

        var answer = DnsAnswerDecoder.Decode(Message(Header(1, 1) + QuestionA + record), DnsRecordType.A);

        Assert.AreEqual(DnsMessageFailure.None, answer.Failure);
        Assert.AreEqual(expected, answer.TimeToLiveSeconds);
    }

    // Family 2, malformed input: a CNAME whose target is a pointer to itself.
    [TestMethod]
    [Timeout(3000, CooperativeCancellation = true)]
    public void DnsAnswerDecode_CnamePointingAtItself_IsLabelLoopWithoutHanging()
    {
        // The CNAME's data starts at offset 12 + 18 + 12 = 42 (0x2A).
        var answer = DnsAnswerDecoder.Decode(Message(Header(1, 1) + QuestionA + "C00C000500010000003C0002C02A"), DnsRecordType.A);

        Assert.AreEqual(DnsMessageFailure.LabelLoop, answer.Failure);
    }

    // Family 2, malformed input: two CNAME targets pointing at each other.
    [TestMethod]
    [Timeout(3000, CooperativeCancellation = true)]
    public void DnsAnswerDecode_TwoCnamesPointingAtEachOther_IsLabelLoopWithoutHanging()
    {
        // The first CNAME's data is at 42 (0x2A); the second record starts at 44 and its data at 56 (0x38).
        var answer = DnsAnswerDecoder.Decode(
            Message(Header(1, 2) + QuestionA + "C00C000500010000003C0002C038" + "C00C000500010000003C0002C02A"),
            DnsRecordType.A);

        Assert.AreEqual(DnsMessageFailure.LabelLoop, answer.Failure);
    }

    // Family 2, malformed input: a CNAME target pointer past the end, one cut in half at the end, and a label longer than the data.
    [TestMethod]
    [DataRow("0002C0FF")]
    [DataRow("0002FFFF")]
    [DataRow("0001C0")]
    [DataRow("0003056100")]
    public void DnsAnswerDecode_CnameTargetRunningPastTheEnd_FailsWithoutThrowing(string lengthAndData)
    {
        var answer = DnsAnswerDecoder.Decode(Message(Header(1, 1) + QuestionA + "C00C000500010000003C" + lengthAndData), DnsRecordType.A);
        TestDiagnostics.For(TestContext).Act("failure", answer.Failure);

        Assert.AreNotEqual(DnsMessageFailure.None, answer.Failure);
    }

    // Family 2, malformed input: a label length byte with a reserved top-bit pattern (01 or 10).
    [TestMethod]
    [DataRow("40")]
    [DataRow("80")]
    [DataRow("BF")]
    public void DnsAnswerDecode_QuestionLabelWithReservedTopBits_IsBadLabel(string lengthByte)
    {
        var answer = DnsAnswerDecoder.Decode(Message(Header(1, 0) + lengthByte + "00" + "00010001"), DnsRecordType.A);

        Assert.AreEqual(DnsMessageFailure.BadLabel, answer.Failure);
    }

    // Family 2, malformed input: every proper prefix of a valid answer, as a truncated body would arrive.
    [TestMethod]
    public void DnsAnswerDecode_ValidAnswerCutAtEveryOffset_FailsWithoutThrowing()
    {
        var whole = Message(Header(1, 1) + QuestionA + AnswerA);

        for (var length = 0; length < whole.Length; length++)
        {
            var answer = DnsAnswerDecoder.Decode(whole.AsSpan(0, length), DnsRecordType.A);
            Assert.AreNotEqual(DnsMessageFailure.None, answer.Failure, $"cut at {length}");
        }

        Assert.AreEqual(DnsMessageFailure.None, DnsAnswerDecoder.Decode(whole, DnsRecordType.A).Failure);
    }

    // Family 2, malformed input: one byte left over after the last record.
    [TestMethod]
    public void DnsAnswerDecode_ByteAfterTheLastRecord_IsMalformed()
    {
        var answer = DnsAnswerDecoder.Decode(Message(Header(1, 1) + QuestionA + AnswerA + "00"), DnsRecordType.A);

        Assert.AreEqual(DnsMessageFailure.Malformed, answer.Failure);
    }

    // Family 2, malformed input: an answer count of 65535 with one record present.
    [TestMethod]
    public void DnsAnswerDecode_AnswerCountFarAboveTheRecordsPresent_IsOutOfRange()
    {
        var answer = DnsAnswerDecoder.Decode(Message(Header(1, 0xFFFF) + QuestionA + AnswerA), DnsRecordType.A);

        Assert.AreEqual(DnsMessageFailure.OutOfRange, answer.Failure);
        Assert.HasCount(1, answer.Addresses);
    }

    // Family 2, malformed input: every single-bit flip of a valid answer, for every record type asked.
    [TestMethod]
    [DataRow(DnsRecordType.A)]
    [DataRow(DnsRecordType.Aaaa)]
    [DataRow(DnsRecordType.Srv)]
    [DataRow(DnsRecordType.Https)]
    public void DnsAnswerDecode_ValidAnswerWithAnyOneBitFlipped_NeverThrows(DnsRecordType askedType)
    {
        var whole = Message(Header(1, 1) + QuestionA + AnswerA);

        for (var bit = 0; bit < whole.Length * 8; bit++)
        {
            var flipped = (byte[])whole.Clone();
            flipped[bit / 8] ^= (byte)(1 << (bit % 8));
            var answer = DnsAnswerDecoder.Decode(flipped, askedType);
            Assert.IsTrue(answer.Addresses.Count <= 24, $"bit {bit}");
        }
    }

    // Family 2, malformed input: seeded random messages with a valid ID, so they pass the first check.
    [TestMethod]
    [Timeout(3000, CooperativeCancellation = true)]
    public void DnsAnswerDecode_SeededRandomMessages_NeverThrowAndNeverExceedTheLimits()
    {
        const int seed = 1502;
        TestDiagnostics.For(TestContext).Arrange("seed", seed);
        var random = new Random(seed);

        for (var round = 0; round < 2000; round++)
        {
            var message = new byte[random.Next(12, 300)];
            random.NextBytes(message);
            message[0] = message[1] = 0;
            message[3] &= 0xF0;
            var askedType = round % 2 == 0 ? DnsRecordType.Https : DnsRecordType.Srv;

            var answer = DnsAnswerDecoder.Decode(message, askedType);

            Assert.IsTrue(answer.Addresses.Count <= 24 && answer.CanonicalNames.Count <= 4 && answer.HttpsRecordData.Count <= 4, $"round {round}");
        }
    }

    // Family 3, invalid partitions: a message ID other than 0.
    [TestMethod]
    [DataRow("0001")]
    [DataRow("FFFF")]
    public void DnsAnswerDecode_NonZeroMessageId_IsBadId(string id)
    {
        var answer = DnsAnswerDecoder.Decode(Message(id + Header(1, 1)[4..] + QuestionA + AnswerA), DnsRecordType.A);

        Assert.AreEqual(DnsMessageFailure.BadId, answer.Failure);
    }

    // Family 3, invalid partitions: every non-zero response code, each its own partition of the low nibble.
    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(15)]
    public void DnsAnswerDecode_NonZeroResponseCode_IsBadRcode(int rcode)
    {
        var answer = DnsAnswerDecoder.Decode(Message("000081" + rcode.ToString("X2") + Header(1, 1)[8..] + QuestionA + AnswerA), DnsRecordType.A);

        Assert.AreEqual(DnsMessageFailure.BadRcode, answer.Failure);
    }

    // Family 3, invalid partitions: an answer of a type not asked for (AAAA, MX) and a class not IN (CH, ANY).
    [TestMethod]
    [DataRow("001C0001", DnsMessageFailure.UnexpectedType)]
    [DataRow("000F0001", DnsMessageFailure.UnexpectedType)]
    [DataRow("00010003", DnsMessageFailure.UnexpectedClass)]
    [DataRow("000100FF", DnsMessageFailure.UnexpectedClass)]
    public void DnsAnswerDecode_AnswerOfTheWrongTypeOrClass_IsRefused(string typeAndClass, DnsMessageFailure expected)
    {
        var record = "C00C" + typeAndClass + "0000003C00047F000001";

        var answer = DnsAnswerDecoder.Decode(Message(Header(1, 1) + QuestionA + record), DnsRecordType.A);

        Assert.AreEqual(expected, answer.Failure);
    }

    // Family 4, state: the decoder keeps its own copy, so changing the body afterwards changes nothing.
    [TestMethod]
    public void DnsAnswerDecode_BodyChangedAfterTheDecode_LeavesTheAnswerAsDecoded()
    {
        const string questionHttps = "076578616D706C6504746573740000410001";
        var message = Message(Header(1, 1) + questionHttps + "C00C004100010000003C0003000100");

        var answer = DnsAnswerDecoder.Decode(message, DnsRecordType.Https);
        Array.Fill(message, (byte)0xFF);

        Assert.AreEqual(DnsMessageFailure.None, answer.Failure);
        CollectionAssert.AreEqual(new byte[] { 0x00, 0x01, 0x00 }, answer.HttpsRecordData[0]);
    }

    // Family 4, concurrency: one message decoded on many threads at once answers as it does alone.
    [TestMethod]
    public async Task DnsAnswerDecode_SameMessageOnManyThreadsAtOnce_AgreesWithOneDecode()
    {
        var message = Message(Header(1, 24) + QuestionA + Repeat(AnswerA, 24));
        var alone = DnsAnswerDecoder.Decode(message, DnsRecordType.A);

        var answers = await Task.WhenAll(Enumerable.Range(0, 64).Select(_ => Task.Run(() => DnsAnswerDecoder.Decode(message, DnsRecordType.A))));

        foreach (var answer in answers)
        {
            Assert.AreEqual(alone.Failure, answer.Failure);
            CollectionAssert.AreEqual(alone.Addresses.ToArray(), answer.Addresses.ToArray());
        }
    }

    // Family 1, boundaries: HTTPS record data one byte short of a priority and a root name, and exactly that.
    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public void ServiceBindingDecode_DataShorterThanPriorityAndRoot_IsTruncated(int length)
    {
        var decoding = ServiceBindingRecordDecoder.Decode(new byte[length]);

        Assert.AreEqual(ServiceBindingFailure.Truncated, decoding.Failure);
        Assert.IsNull(decoding.Record);
    }

    [TestMethod]
    public void ServiceBindingDecode_PriorityAndRootOnly_DecodesToTheRootTarget()
    {
        var decoding = ServiceBindingRecordDecoder.Decode(Message("FFFF00"));

        Assert.AreEqual(ServiceBindingFailure.None, decoding.Failure);
        Assert.AreEqual((ushort)65535, decoding.Record!.Priority);
        Assert.AreEqual(".", decoding.Record.TargetName);
    }

    // Family 1, boundaries: a target name label of 63 bytes, the most RFC 1035 allows, and of 64.
    [TestMethod]
    [DataRow(63, ServiceBindingFailure.None)]
    [DataRow(64, ServiceBindingFailure.BadTargetName)]
    public void ServiceBindingDecode_TargetLabelAroundSixtyThreeBytes_RefusesOnlyTheLongerOne(int labelLength, ServiceBindingFailure expected)
    {
        var decoding = ServiceBindingRecordDecoder.Decode(Message("0001" + labelLength.ToString("X2") + Repeat("61", labelLength) + "00"));

        Assert.AreEqual(expected, decoding.Failure);
    }

    // Family 1, boundaries: ipv4hint and port values one byte off their sizes, and none at all.
    [TestMethod]
    [DataRow("00040000")]
    [DataRow("0004000301020304")]
    [DataRow("000400050102030405")]
    [DataRow("0003000150")]
    [DataRow("00030003000050")]
    [DataRow("00010000")]
    [DataRow("0001000100")]
    [DataRow("000200015A")]
    public void ServiceBindingDecode_KnownParameterOfTheWrongShape_IsBadParameterValue(string parameter)
    {
        var decoding = ServiceBindingRecordDecoder.Decode(Message("000100" + parameter));
        TestDiagnostics.For(TestContext).Act("failure", decoding.Failure);

        Assert.AreEqual(ServiceBindingFailure.BadParameterValue, decoding.Failure);
    }

    // Family 2, malformed input: a compressed target name, which RFC 9460 section 2.2 forbids.
    [TestMethod]
    [DataRow("C00C")]
    [DataRow("4000")]
    [DataRow("8000")]
    public void ServiceBindingDecode_CompressedOrReservedTargetLabel_IsBadTargetName(string label)
    {
        var decoding = ServiceBindingRecordDecoder.Decode(Message("0001" + label));

        Assert.AreEqual(ServiceBindingFailure.BadTargetName, decoding.Failure);
    }

    // Family 2, malformed input: a parameter length of 65535 with two bytes of value.
    [TestMethod]
    public void ServiceBindingDecode_ParameterLengthPastTheEnd_IsParameterOverrun()
    {
        var decoding = ServiceBindingRecordDecoder.Decode(Message("000100" + "0001FFFF0268"));

        Assert.AreEqual(ServiceBindingFailure.ParameterOverrun, decoding.Failure);
    }

    // Family 2, malformed input: every proper prefix of a valid record with alpn, port and hints.
    [TestMethod]
    public void ServiceBindingDecode_ValidRecordCutAtEveryOffset_FailsOrDecodesWithoutThrowing()
    {
        var whole = Message("0001" + "0378797A00" + "000100030268320003000201BB000400047F000001");
        var complete = ServiceBindingRecordDecoder.Decode(whole);

        for (var length = 0; length < whole.Length; length++)
        {
            var decoding = ServiceBindingRecordDecoder.Decode(whole.AsSpan(0, length));
            Assert.AreEqual(decoding.Failure == ServiceBindingFailure.None, decoding.Record is not null, $"cut at {length}");
            // A cut inside the target name or a parameter never decodes.
            Assert.IsFalse(length is > 2 and < 7 && decoding.Failure == ServiceBindingFailure.None, $"cut at {length}");
        }

        Assert.AreEqual(ServiceBindingFailure.None, complete.Failure);
        CollectionAssert.AreEqual(new[] { "h2" }, complete.Record!.ApplicationProtocols.ToArray());
        Assert.AreEqual((ushort)443, complete.Record.Port);
    }

    // Family 2, malformed input: seeded random record data.
    [TestMethod]
    [Timeout(3000, CooperativeCancellation = true)]
    public void ServiceBindingDecode_SeededRandomData_NeverThrowsAndRecordMatchesFailure()
    {
        const int seed = 9460;
        TestDiagnostics.For(TestContext).Arrange("seed", seed);
        var random = new Random(seed);

        for (var round = 0; round < 2000; round++)
        {
            var data = new byte[random.Next(0, 200)];
            random.NextBytes(data);
            if (data.Length > 2)
            {
                data[2] = 0;
            }

            var decoding = ServiceBindingRecordDecoder.Decode(data);

            Assert.AreEqual(decoding.Failure == ServiceBindingFailure.None, decoding.Record is not null, $"round {round}");
        }
    }

    // Family 1, boundaries: --resolve ports 0 and 65535 parse; 65536 fails (measured: exit 49).
    [TestMethod]
    [DataRow("h:0:1.2.3.4", 0)]
    [DataRow("h:65535:1.2.3.4", 65535)]
    public void ResolveParse_PortAtTheEdgesOfItsRange_Parses(string entry, int port)
    {
        var overrides = ResolveOverrides.Parse([entry]);

        Assert.IsNull(overrides.ParseError);
        CollectionAssert.AreEqual(new[] { IPAddress.Parse("1.2.3.4") }, overrides.Find("h", port)!.ToArray());
    }

    // Family 3, invalid partitions of --resolve: port above range, port empty, address list empty,
    // only commas, one address too many parts, a zone, and one bad item after a good one.
    // Measured on curl 8.21.0 (Windows, Schannel): each exits 49 with this text.
    [TestMethod]
    [DataRow("h:65536:1.2.3.4")]
    [DataRow("h::1.2.3.4")]
    [DataRow("h:80:")]
    [DataRow("h:80:,,")]
    [DataRow("h:80:1.2.3.4.5")]
    [DataRow("h:80:fe80::1%eth0")]
    [DataRow("h:80:1.2.3.4,bad")]
    public void ResolveParse_EntryInAnInvalidPartition_FailsWithCurlsExit49Text(string entry)
    {
        var overrides = ResolveOverrides.Parse(["good:80:1.1.1.1", entry, "after:80:2.2.2.2"]);

        Assert.AreEqual($"Could not parse CURLOPT_RESOLVE entry '{entry}'", overrides.ParseError);
        Assert.HasCount(1, overrides.Entries);
        Assert.IsNull(overrides.Find("after", 80));
    }

    // Family 4, state: a removal, a re-add and a removal of something never added, in that order.
    [TestMethod]
    public void ResolveParse_AddRemoveAndReAddOfOneHost_EndsWithTheLastAddition()
    {
        var overrides = ResolveOverrides.Parse(["H:80:1.1.1.1", "-h:80", "-never:80", "h:80:[::1],2.2.2.2"]);

        Assert.IsNull(overrides.ParseError);
        CollectionAssert.AreEqual(new[] { IPAddress.IPv6Loopback, IPAddress.Parse("2.2.2.2") }, overrides.Find("[H]", 80)!.ToArray());
    }

    // Family 4, concurrency: one set of overrides read from many threads at once.
    [TestMethod]
    public async Task ResolveFind_FromManyThreadsAtOnce_AgreesWithSequentialAnswers()
    {
        var overrides = ResolveOverrides.Parse(["a:80:1.1.1.1", "*:443:2.2.2.2", "[::1]:8080:3.3.3.3"]);
        (string Host, int Port)[] queries = [("a", 80), ("A", 80), ("other", 443), ("::1", 8080), ("[::1]", 8080), ("a", 81)];
        var expected = queries.Select(query => overrides.Find(query.Host, query.Port)?.SingleOrDefault()).ToArray();

        var results = await Task.WhenAll(Enumerable.Range(0, 256).Select(index => Task.Run(() =>
        {
            var query = queries[index % queries.Length];
            return (Index: index % queries.Length, Address: overrides.Find(query.Host, query.Port)?.SingleOrDefault());
        })));

        foreach (var (index, address) in results)
        {
            Assert.AreEqual(expected[index], address);
        }
    }

    // Family 1, boundaries: a --connect-to destination port of 65535 maps; 65536 fails (measured: exit 49).
    [TestMethod]
    public void ConnectToMap_DestinationPortAtTheTopOfItsRange_Maps()
    {
        var destination = new ConnectToMappings(["h:1:x:65535"]).Map("h", 1);

        Assert.AreEqual(new ConnectDestination("x", 65535, IsMapped: true, ParseError: null), destination);
    }

    // Family 3, invalid partitions of a --connect-to destination: port above range, unclosed IPv6 bracket.
    // Measured on curl 8.21.0 (Windows, Schannel): each exits 49 with this text.
    [TestMethod]
    [DataRow("h:1:x:65536", "No valid port number in 'x:65536'")]
    [DataRow("h:1:[::1:9", "Invalid IPv6 address format in '[::1:9'")]
    public void ConnectToMap_DestinationInAnInvalidPartition_FailsWithCurlsExit49Text(string mapping, string expected)
    {
        var destination = new ConnectToMappings([mapping]).Map("h", 1);

        Assert.AreEqual(expected, destination.ParseError);
        Assert.IsFalse(destination.IsMapped);
    }

    // Family 3, invalid partitions of a --connect-to source: no port field, an unclosed bracket, an empty value.
    [TestMethod]
    [DataRow("h")]
    [DataRow("h:1")]
    [DataRow("[::1:1:x:9")]
    [DataRow("")]
    public void ConnectToMap_MappingWithoutAWholeSource_LeavesTheConnectionUnmapped(string mapping)
    {
        var destination = new ConnectToMappings([mapping]).Map("h", 1);

        Assert.AreEqual(new ConnectDestination("h", 1, IsMapped: false, ParseError: null), destination);
    }

    // Family 4, concurrency: one set of mappings read from many threads at once.
    [TestMethod]
    public async Task ConnectToMap_FromManyThreadsAtOnce_AgreesWithSequentialAnswers()
    {
        var mappings = new ConnectToMappings(["a:80:b:81", "::c:", "[::1]:443:[::2]:444"]);
        (string Host, int Port)[] queries = [("a", 80), ("A", 80), ("z", 9), ("[::1]", 443), ("::1", 443)];
        var expected = queries.Select(query => mappings.Map(query.Host, query.Port)).ToArray();

        var results = await Task.WhenAll(Enumerable.Range(0, 256).Select(index => Task.Run(() =>
        {
            var query = queries[index % queries.Length];
            return (Index: index % queries.Length, Destination: mappings.Map(query.Host, query.Port));
        })));

        foreach (var (index, destination) in results)
        {
            Assert.AreEqual(expected[index], destination);
        }
    }

    /// <summary>A DoH response header: ID 0, flags 8180, one question count and one answer count.</summary>
    private static string Header(int questions, int answers) => $"00008180{questions:X4}{answers:X4}00000000";

    private static string Repeat(string hex, int count) => string.Concat(Enumerable.Repeat(hex, count));

    private static byte[] Message(string hex) => Convert.FromHexString(hex);
}
