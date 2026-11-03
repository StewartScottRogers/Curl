using System.Buffers.Binary;
using System.Text;
using Curl.Conformance.Extended.Amqp1;
using Curl.Conformance.Extended.Harness;
using static Curl.Conformance.Extended.UnitTests.Amqp1.Amqp1ProtocolHeaderCases;
using static Curl.Conformance.Extended.UnitTests.Amqp1.Amqp1SessionLinkCases;

namespace Curl.Conformance.Extended.UnitTests.Amqp1;

/// <summary>
/// Part 1 sections 1.3 to 1.6: every encoding the container may choose for a value curl reads, from sasl-mechanisms to
/// close: the widest forms, the forms that skip the zero-width shortcuts, symbolic descriptors and a single symbol
/// where a field is multiple.
/// </summary>
[TestClass]
public sealed class Amqp1EncodingWidthCases
{
    private static readonly ulong?[] SentMessageOrder =
    [
        Amqp1ReferenceDescriptors.SaslInit, Amqp1ReferenceDescriptors.Open, Amqp1ReferenceDescriptors.Begin, Amqp1ReferenceDescriptors.Attach,
        Amqp1ReferenceDescriptors.Transfer, Amqp1ReferenceDescriptors.Detach, Amqp1ReferenceDescriptors.End, Amqp1ReferenceDescriptors.Close,
    ];

    [TestMethod]
    [SpecRequirement("Amqp1:Types-1.4-1")]
    [SpecRequirement("Amqp1:Types-1.6-1")]
    [SpecRequirement("Amqp1:Types-1.6-2")]
    [SpecRequirement("Amqp1:Types-1.6-3")]
    [SpecRequirement("Amqp1:Types-1.6-4")]
    public Task Widest_ContainerWritesEveryLargeForm_CurlSendsTheMessage() =>
        Known.RunCaseAsync(["Amqp1:Types-1.4-1", "Amqp1:Types-1.6-1", "Amqp1:Types-1.6-2", "Amqp1:Types-1.6-3", "Amqp1:Types-1.6-4"], () =>
            AssertMessageSentAsync(new Amqp1ReferenceContainerOptions { WideEncoding = true, LinkBehaviour = new AcceptingLink() }));

    [TestMethod]
    [SpecRequirement("Amqp1:Types-1.6-2")]
    public Task Widest_DetachErrorInStr32AndSym32_ReportsTheErrorWithExit78() =>
        Known.RunCaseAsync(["Amqp1:Types-1.6-2"], () => AssertLinkRefusedAsync(new Amqp1ReferenceContainerOptions { WideEncoding = true }));

    [TestMethod]
    [SpecRequirement("Amqp1:Types-1.6-1")]
    [SpecRequirement("Amqp1:Types-1.6-3")]
    public Task ZeroWidthFormsSkipped_SmallUintBooleanByteAndList8_CurlSendsTheMessage() =>
        Known.RunCaseAsync(["Amqp1:Types-1.6-1", "Amqp1:Types-1.6-3"], () =>
            AssertMessageSentAsync(new Amqp1ReferenceContainerOptions
            {
                BodyEncoding = new AlternateForms { SkipZeroWidthForms = true, BooleanWithByte = true }.Encode,
                LinkBehaviour = new AcceptingLink(),
            }));

    [TestMethod]
    [SpecRequirement("Amqp1:Types-1.6-3")]
    public Task List32WithSmallScalars_EveryListInList32_CurlSendsTheMessage() =>
        Known.RunCaseAsync(["Amqp1:Types-1.6-3"], () =>
            AssertMessageSentAsync(new Amqp1ReferenceContainerOptions
            {
                BodyEncoding = new AlternateForms { ListsAsList32 = true }.Encode,
                LinkBehaviour = new AcceptingLink(),
            }));

    [TestMethod]
    [SpecRequirement("Amqp1:Types-1.5-1")]
    public Task SymbolicDescriptors_EveryBodyDescribedByName_CurlSendsTheMessage() =>
        Known.RunCaseAsync(["Amqp1:Types-1.5-1"], () =>
            AssertMessageSentAsync(new Amqp1ReferenceContainerOptions
            {
                BodyEncoding = new AlternateForms { SymbolicDescriptors = true }.Encode,
                LinkBehaviour = new AcceptingLink(),
            }));

    [TestMethod]
    [SpecRequirement("Amqp1:Types-1.5-1")]
    public Task SymbolicDescriptors_DetachErrorDescribedByName_ReportsTheErrorWithExit78() =>
        Known.RunCaseAsync(["Amqp1:Types-1.5-1"], () =>
            AssertLinkRefusedAsync(new Amqp1ReferenceContainerOptions { BodyEncoding = new AlternateForms { SymbolicDescriptors = true }.Encode }));

    [TestMethod]
    [SpecRequirement("Amqp1:Types-1.3-1")]
    public Task MultipleField_SingleSymbolForServerMechanisms_InitsThatMechanism() =>
        Known.RunCaseAsync(["Amqp1:Types-1.3-1"], async () =>
        {
            Amqp1ReferenceContainer container = new(new Amqp1ReferenceContainerOptions
            {
                SaslMechanisms = ["ANONYMOUS"],
                BodyEncoding = new AlternateForms { SingleSymbolForArray = true }.Encode,
            });

            CurlRunResult result = await RunAsync(container, "-d", "hi");

            Assert.AreEqual(78, result.ExitCode);
            Assert.AreEqual(RefusedLinkError, Text(result.StandardError));
            Assert.AreEqual(new Amqp1ReferenceSymbol("ANONYMOUS"), container.Frames[0].Body!.Field(0));
        });

    private static async Task AssertMessageSentAsync(Amqp1ReferenceContainerOptions options)
    {
        Amqp1ReferenceContainer container = new(options);

        CurlRunResult result = await RunAsync(container, "-d", "hi");

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.IsEmpty(result.StandardError);
        Assert.IsEmpty(result.StandardOutput);
        CollectionAssert.AreEqual(SentMessageOrder, Descriptors(container));
        Assert.EndsWith("hi", Text(container.Frames[4].Payload), "the transfer carries the message");
        List<byte[]> frames = ClientAmqpFrames(result.Exchanges[0].ClientBytes);
        Assert.HasCount(7, frames);
        Assert.AreEqual(Amqp1ReferenceDescriptors.Close, (ulong)frames[^1][10], "the close is the last frame curl writes");
    }

    private static async Task AssertLinkRefusedAsync(Amqp1ReferenceContainerOptions options)
    {
        Amqp1ReferenceContainer container = new(options);

        CurlRunResult result = await RunAsync(container, "-d", "hi");

        Assert.AreEqual(78, result.ExitCode);
        Assert.AreEqual(RefusedLinkError, Text(result.StandardError));
        Assert.IsEmpty(result.StandardOutput);
        Assert.DoesNotContain((ulong?)Amqp1ReferenceDescriptors.Transfer, Descriptors(container));
    }

    /// <summary>
    /// Writes a frame body in the forms Part 1 section 1.6 allows besides the smallest: each choice switches one family of
    /// forms, and every other value is left to the container's smallest encodings.
    /// </summary>
    private sealed class AlternateForms
    {
        private static readonly Amqp1ReferenceEncoder Smallest = new(wide: false);

        private static readonly Dictionary<ulong, string> DescriptorNames = new()
        {
            [0x10] = "amqp:open:list",
            [0x11] = "amqp:begin:list",
            [0x12] = "amqp:attach:list",
            [0x13] = "amqp:flow:list",
            [0x14] = "amqp:transfer:list",
            [0x15] = "amqp:disposition:list",
            [0x16] = "amqp:detach:list",
            [0x17] = "amqp:end:list",
            [0x18] = "amqp:close:list",
            [0x1d] = "amqp:error:list",
            [0x24] = "amqp:accepted:list",
            [0x28] = "amqp:source:list",
            [0x29] = "amqp:target:list",
            [0x40] = "amqp:sasl-mechanisms:list",
            [0x44] = "amqp:sasl-outcome:list",
        };

        /// <summary>Gets a value indicating whether a uint zero is written as smalluint and an empty list as list8, not uint0 and list0.</summary>
        public bool SkipZeroWidthForms { get; init; }

        /// <summary>Gets a value indicating whether a boolean is written as 0x56 and a byte, not true or false.</summary>
        public bool BooleanWithByte { get; init; }

        /// <summary>Gets a value indicating whether every list is written as list32.</summary>
        public bool ListsAsList32 { get; init; }

        /// <summary>Gets a value indicating whether every descriptor is written as its symbolic name.</summary>
        public bool SymbolicDescriptors { get; init; }

        /// <summary>Gets a value indicating whether an array of one symbol is written as that symbol alone.</summary>
        public bool SingleSymbolForArray { get; init; }

        public byte[] Encode(Amqp1ReferenceDescribed body)
        {
            List<byte> output = [];
            Write(output, body);
            return [.. output];
        }

        private void Write(List<byte> output, object? value)
        {
            switch (value)
            {
                case Amqp1ReferenceDescribed described:
                    output.Add(0x00);
                    output.AddRange(SymbolicDescriptors ? Smallest.Encode(new Amqp1ReferenceSymbol(DescriptorNames[described.Descriptor])) : Smallest.Encode(described.Descriptor));
                    Write(output, described.Value);
                    break;
                case bool flag when BooleanWithByte: output.AddRange([0x56, flag ? (byte)1 : (byte)0]); break;
                case 0u when SkipZeroWidthForms: output.AddRange([0x52, 0]); break;
                case Amqp1ReferenceSymbol[] { Length: 1 } symbols when SingleSymbolForArray: Write(output, symbols[0]); break;
                case Amqp1ReferenceSymbol[] or byte[] or string: output.AddRange(Smallest.Encode(value)); break;
                case IReadOnlyList<object?> list: WriteList(output, list); break;
                default: output.AddRange(Smallest.Encode(value)); break;
            }
        }

        private void WriteList(List<byte> output, IReadOnlyList<object?> list)
        {
            List<byte> items = [];
            foreach (object? item in list)
            {
                Write(items, item);
            }

            if (list.Count == 0 && !SkipZeroWidthForms && !ListsAsList32)
            {
                output.Add(0x45);
            }
            else if (!ListsAsList32 && items.Count + 1 <= byte.MaxValue)
            {
                output.AddRange([0xc0, (byte)(items.Count + 1), (byte)list.Count]);
            }
            else
            {
                byte[] header = new byte[9];
                header[0] = 0xd0;
                BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(1), (uint)(items.Count + 4));
                BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(5), (uint)list.Count);
                output.AddRange(header);
            }

            output.AddRange(items);
        }
    }
}
