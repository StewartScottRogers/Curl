using System.Text;

namespace Curl.Networking;

internal sealed partial class DnsAnswerReader
{
    private const int MaximumNameSteps = 128;

    /// <summary>
    /// Reads the name at <paramref name="position" /> into <paramref name="name" />, following
    /// compression pointers; like curl's <c>doh_store_cname</c>, it gives up after 128 labels
    /// and pointers, which is how a pointer loop ends.
    /// </summary>
    private DnsMessageFailure FollowName(int position, StringBuilder name)
    {
        var steps = MaximumNameSteps;
        int length;
        DnsMessageFailure failure;
        do
        {
            failure = FollowNameStep(ref position, name, out length);
        }
        while (failure == DnsMessageFailure.None && length != 0 && --steps != 0);

        return failure == DnsMessageFailure.None && steps == 0 ? DnsMessageFailure.LabelLoop : failure;
    }

    /// <summary>Reads one label or pointer of a name; <paramref name="length" /> is its first byte, 0 at the root.</summary>
    private DnsMessageFailure FollowNameStep(ref int position, StringBuilder name, out int length)
    {
        length = 0;
        if (position >= message.Length)
        {
            return DnsMessageFailure.OutOfRange;
        }

        length = message[position];
        return (length & 0xC0) switch
        {
            0xC0 => FollowPointer(ref position),
            0x00 => AppendLabel(ref position, length, name),
            _ => DnsMessageFailure.BadLabel,
        };
    }

    private DnsMessageFailure FollowPointer(ref int position)
    {
        if (position + 1 >= message.Length)
        {
            return DnsMessageFailure.OutOfRange;
        }

        position = ((message[position] & 0x3F) << 8) | message[position + 1];
        return DnsMessageFailure.None;
    }

    private DnsMessageFailure AppendLabel(ref int position, int length, StringBuilder name)
    {
        position++;
        if (length == 0)
        {
            return DnsMessageFailure.None;
        }

        if (position + length > message.Length)
        {
            return DnsMessageFailure.BadLabel;
        }

        if (name.Length > 0)
        {
            name.Append('.');
        }

        name.Append(Encoding.Latin1.GetString(message, position, length));
        position += length;
        return DnsMessageFailure.None;
    }

    /// <summary>Skips the name at the read position, stopping after its first pointer, as curl's <c>doh_skipqname</c> does.</summary>
    private DnsMessageFailure SkipName()
    {
        int length;
        var failure = DnsMessageFailure.None;
        do
        {
            if (!Has(1))
            {
                return DnsMessageFailure.OutOfRange;
            }

            length = message[_index];
            (failure, length) = (length & 0xC0) switch
            {
                0xC0 => (Advance(2), 0),
                0x00 => (Advance(1 + length), length),
                _ => (DnsMessageFailure.BadLabel, 0),
            };
        }
        while (failure == DnsMessageFailure.None && length != 0);

        return failure;
    }
}
