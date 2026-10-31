using System.Text;
using Curl.Extension.Dane.Dnssec;
using Curl.Extension.Dane.Wire;

namespace Curl.Extension.Dane.Lookup;

/// <summary>
/// Builds, cuts and orders the <see cref="DnsName" />s the lookup walks: names from text, their ancestors, a label
/// put in front, and RFC 4034 section 6.1's canonical order NSEC denial relies on.
/// </summary>
internal static class LookupNames
{
    private const int MaximumLabelLength = 63;

    /// <summary>The root name.</summary>
    public static readonly DnsName Root = new([0], 0);

    /// <summary>Encodes a dotted name; a trailing dot is optional.</summary>
    /// <param name="text">The name, such as <c>_443._tcp.example.com</c>.</param>
    /// <returns>The name.</returns>
    /// <exception cref="ArgumentException">A label is empty or over 63 octets.</exception>
    public static DnsName FromText(string text)
    {
        DnsName name = Root;
        string trimmed = text.EndsWith('.') ? text[..^1] : text;
        foreach (string label in trimmed.Split('.').Reverse())
        {
            byte[] octets = Encoding.ASCII.GetBytes(label);
            if (octets.Length is 0 or > MaximumLabelLength)
            {
                throw new ArgumentException($"The label '{label}' is not 1 to {MaximumLabelLength} octets long.", nameof(text));
            }

            name = Prepend(octets, name);
        }

        return name;
    }

    /// <summary>Puts <paramref name="label" /> in front of <paramref name="name" />.</summary>
    /// <param name="label">The new leftmost label's octets.</param>
    /// <param name="name">The parent name.</param>
    /// <returns>The child name.</returns>
    public static DnsName Prepend(byte[] label, DnsName name) =>
        new([(byte)label.Length, .. label, .. name.Wire.Span], name.LabelCount + 1);

    /// <summary>The ancestor of <paramref name="name" /> with <paramref name="labelCount" /> labels.</summary>
    /// <param name="name">The name.</param>
    /// <param name="labelCount">The ancestor's label count, at most the name's.</param>
    /// <returns>The ancestor; the name itself when the counts are equal.</returns>
    public static DnsName Ancestor(DnsName name, int labelCount) =>
        new(DnssecCanonicalForm.DropLeftmostLabels(name.Wire.Span, name.LabelCount - labelCount).ToArray(), labelCount);

    /// <summary>The leftmost label's octets.</summary>
    /// <param name="name">A name below the root.</param>
    /// <returns>The label.</returns>
    public static byte[] FirstLabel(DnsName name) => name.Wire.Span.Slice(1, name.Wire.Span[0]).ToArray();

    /// <summary>The deepest name both <paramref name="first" /> and <paramref name="second" /> are at or below.</summary>
    /// <param name="first">One name.</param>
    /// <param name="second">The other name.</param>
    /// <returns>Their closest common ancestor.</returns>
    public static DnsName CommonAncestor(DnsName first, DnsName second)
    {
        List<byte[]> firstLabels = CanonicalLabelsFromRight(first);
        List<byte[]> secondLabels = CanonicalLabelsFromRight(second);
        int shared = 0;
        while (shared < Math.Min(firstLabels.Count, secondLabels.Count) &&
               firstLabels[shared].AsSpan().SequenceEqual(secondLabels[shared]))
        {
            shared++;
        }

        return Ancestor(first, shared);
    }

    /// <summary>Compares two names in RFC 4034 section 6.1's canonical order.</summary>
    /// <param name="first">One name.</param>
    /// <param name="second">The other name.</param>
    /// <returns>Less than zero when <paramref name="first" /> sorts first, zero when they are the same name.</returns>
    public static int CompareCanonical(DnsName first, DnsName second)
    {
        List<byte[]> firstLabels = CanonicalLabelsFromRight(first);
        List<byte[]> secondLabels = CanonicalLabelsFromRight(second);
        for (int index = 0; index < Math.Min(firstLabels.Count, secondLabels.Count); index++)
        {
            int order = firstLabels[index].AsSpan().SequenceCompareTo(secondLabels[index]);
            if (order != 0)
            {
                return order;
            }
        }

        return firstLabels.Count.CompareTo(secondLabels.Count);
    }

    // The name's labels lowercased, the rightmost first.
    private static List<byte[]> CanonicalLabelsFromRight(DnsName name)
    {
        byte[] wire = DnssecCanonicalForm.LowercaseName(name.Wire.Span);
        List<byte[]> labels = [];
        for (int offset = 0; wire[offset] != 0; offset += wire[offset] + 1)
        {
            labels.Insert(0, wire[(offset + 1)..(offset + 1 + wire[offset])]);
        }

        return labels;
    }
}
