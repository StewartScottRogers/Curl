using System.Text;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Maps a host name that is not ASCII as IDNA (UTS #46, non-transitional, Unicode 15.1) does before
/// it is converted to punycode, as libidn2 2.3.8 does for curl 8.22.0: every character the IDNA
/// mapping table maps (including <c>disallowed_STD3_mapped</c>, refused only in a label that stays
/// outside ASCII, ADR-1000) is replaced by its mapping, ignored characters are dropped, and the result is put in
/// Normalization Form C (canonical decomposition, canonical reordering and composition, Hangul
/// included). <c>ß</c> and final sigma (deviations) and disallowed characters stay as they are.
/// Curl runs with invariant globalization, where <see cref="System.Globalization.IdnMapping" /> and
/// <see cref="string.Normalize()" /> map nothing, so the mapping is done here from
/// <see cref="CurlUrlHostNameMappingTable" /> (ADR-0816, ADR-0980).
/// </summary>
internal static class CurlUrlHostNameMapping
{
    /// <summary>
    /// The characters dropped before the table is read: soft hyphen, combining grapheme joiner,
    /// Mongolian variation selectors and vowel separator, zero-width space, non-joiner and joiner,
    /// word joiner, variation selectors and the byte order mark. curl 8.22.0 drops the joiners too,
    /// as its transitional retry maps them to nothing (their context rules are BL-3588's).
    /// </summary>
    public const string CharactersMappedToNothing =
        "­͏᠋᠌᠍᠎᠏​‌‍⁠"
        + "︀︁︂︃︄︅︆︇︈︉︊︋︌︍︎️﻿";

    private const string ReplacementCharacterText = "�";

    private const int HangulSyllableBase = 0xAC00;

    private const int HangulLeadingBase = 0x1100;

    private const int HangulVowelBase = 0x1161;

    private const int HangulTrailingBase = 0x11A7;

    private const int HangulLeadingCount = 19;

    private const int HangulVowelCount = 21;

    private const int HangulTrailingCount = 28;

    private const int HangulVowelSyllables = HangulVowelCount * HangulTrailingCount;

    private const int HangulSyllableCount = HangulLeadingCount * HangulVowelSyllables;

    private static readonly Dictionary<int, string> Mappings = ReadMappings();

    private static readonly Dictionary<int, int> CombiningClasses = ReadCombiningClasses();

    private static readonly Dictionary<int, int[]> Decompositions = [];

    private static readonly Dictionary<long, int> Compositions = ReadDecompositions();

    /// <summary>Maps <paramref name="name" /> as UTS #46 does; see the class summary.</summary>
    public static string Map(string name)
    {
        var mapped = new StringBuilder(name.Length);
        var mappingsOutsideStd3Rules = new List<int>();
        foreach (Rune character in name.EnumerateRunes())
        {
            if (character.IsBmp && CharactersMappedToNothing.Contains((char)character.Value))
            {
                continue;
            }

            if (Mappings.TryGetValue(character.Value, out string? mapping))
            {
                AddIfOutsideStd3Rules(mappingsOutsideStd3Rules, mapping, mapped.Length);
                mapped.Append(mapping);
            }
            else
            {
                mapped.Append(character.ToString());
            }
        }

        RefuseMappingsOutsideStd3RulesInLabelsNotAscii(mapped, mappingsOutsideStd3Rules);

        return Compose(Decompose(mapped.ToString()));
    }

    /// <summary>
    /// Records that the mapping appended at <paramref name="index" /> holds an ASCII character other
    /// than a letter, digit, hyphen or dot (U+2100 maps to <c>a/c</c>, U+FDFA to text with spaces).
    /// </summary>
    private static void AddIfOutsideStd3Rules(List<int> mappingsOutsideStd3Rules, string mapping, int index)
    {
        if (mapping.Any(character => character < 0x80 && !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '.')))
        {
            mappingsOutsideStd3Rules.Add(index);
        }
    }

    /// <summary>
    /// Replaces the first character of each mapping outside the STD3 rules with U+FFFD, so the host is
    /// refused, when the label it lands in holds a character outside ASCII once mapped: libidn2 2.3.8
    /// passes a label that maps to ASCII as it is (<c>a℀b</c> is <c>aa/cb</c>, <c>a︐b</c> is
    /// <c>a,b</c>), but refuses such a mapping in a label it converts to punycode (<c>a℀é</c>,
    /// <c>aﹰb</c>, whose mapping is a space and an Arabic mark), while <c>a℀.é</c> is
    /// <c>aa/c.xn--9ca</c> (BL-4076, BL-4079).
    /// </summary>
    private static void RefuseMappingsOutsideStd3RulesInLabelsNotAscii(StringBuilder mapped, List<int> mappingsOutsideStd3Rules)
    {
        string text = mapped.ToString();
        foreach (int index in mappingsOutsideStd3Rules.Where(index => LabelAt(text, index).ContainsAnyInRange('\u0080', char.MaxValue)))
        {
            mapped[index] = ReplacementCharacterText[0];
        }
    }

    private static ReadOnlySpan<char> LabelAt(string text, int index)
    {
        int start = text.LastIndexOf('.', index) + 1;
        int end = text.IndexOf('.', index);

        return text.AsSpan(start, (end < 0 ? text.Length : end) - start);
    }

    private static List<int> Decompose(string mapped)
    {
        var decomposed = new List<int>(mapped.Length);
        foreach (Rune character in mapped.EnumerateRunes())
        {
            AppendDecomposition(decomposed, character.Value);
        }

        return decomposed;
    }

    private static void AppendDecomposition(List<int> decomposed, int codePoint)
    {
        int syllableIndex = codePoint - HangulSyllableBase;
        if ((uint)syllableIndex < HangulSyllableCount)
        {
            AppendHangulDecomposition(decomposed, syllableIndex);
        }
        else if (Decompositions.TryGetValue(codePoint, out int[]? parts))
        {
            foreach (int part in parts)
            {
                AppendDecomposition(decomposed, part);
            }
        }
        else
        {
            AppendInCanonicalOrder(decomposed, codePoint);
        }
    }

    private static void AppendHangulDecomposition(List<int> decomposed, int syllableIndex)
    {
        decomposed.Add(HangulLeadingBase + (syllableIndex / HangulVowelSyllables));
        decomposed.Add(HangulVowelBase + (syllableIndex % HangulVowelSyllables / HangulTrailingCount));
        int trailingIndex = syllableIndex % HangulTrailingCount;
        if (trailingIndex != 0)
        {
            decomposed.Add(HangulTrailingBase + trailingIndex);
        }
    }

    private static void AppendInCanonicalOrder(List<int> decomposed, int codePoint)
    {
        int combiningClass = CombiningClassOf(codePoint);
        int index = decomposed.Count;
        while (combiningClass != 0 && index > 0 && CombiningClassOf(decomposed[index - 1]) > combiningClass)
        {
            index--;
        }

        decomposed.Insert(index, codePoint);
    }

    private static string Compose(List<int> decomposed)
    {
        var composed = new List<int>(decomposed.Count);
        int starterIndex = -1;
        int lastClass = 0;
        foreach (int codePoint in decomposed)
        {
            int combiningClass = CombiningClassOf(codePoint);
            if (!IsBlocked(composed, starterIndex, lastClass, combiningClass) && TryCompose(composed[starterIndex], codePoint, out int composite))
            {
                composed[starterIndex] = composite;
                continue;
            }

            starterIndex = combiningClass == 0 ? composed.Count : starterIndex;
            lastClass = combiningClass;
            composed.Add(codePoint);
        }

        return ToText(composed);
    }

    private static bool IsBlocked(List<int> composed, int starterIndex, int lastClass, int combiningClass) =>
        starterIndex < 0 || (composed.Count - 1 != starterIndex && lastClass >= combiningClass);

    private static string ToText(List<int> composed)
    {
        var text = new StringBuilder(composed.Count);
        foreach (int codePoint in composed)
        {
            text.Append(char.ConvertFromUtf32(codePoint));
        }

        return text.ToString();
    }

    private static bool TryCompose(int starter, int codePoint, out int composite)
    {
        int leadingIndex = starter - HangulLeadingBase;
        int vowelIndex = codePoint - HangulVowelBase;
        if ((uint)leadingIndex < HangulLeadingCount && (uint)vowelIndex < HangulVowelCount)
        {
            composite = HangulSyllableBase + (((leadingIndex * HangulVowelCount) + vowelIndex) * HangulTrailingCount);
            return true;
        }

        int syllableIndex = starter - HangulSyllableBase;
        int trailingIndex = codePoint - HangulTrailingBase;
        if ((uint)syllableIndex < HangulSyllableCount && syllableIndex % HangulTrailingCount == 0 && (uint)(trailingIndex - 1) < HangulTrailingCount - 1)
        {
            composite = starter + trailingIndex;
            return true;
        }

        return Compositions.TryGetValue(CompositionKey(starter, codePoint), out composite);
    }

    private static int CombiningClassOf(int codePoint) => CombiningClasses.GetValueOrDefault(codePoint);

    private static long CompositionKey(int starter, int mark) => ((long)starter << 21) | (uint)mark;

    private static Dictionary<int, string> ReadMappings()
    {
        var mappings = new Dictionary<int, string>();
        var reader = new TableReader(CurlUrlHostNameMappingTable.Mappings);
        int start = 0;
        while (!reader.IsAtEnd)
        {
            start += reader.ReadNumber();
            int end = start + reader.ReadNumber();
            int[] mapping = reader.ReadCodePoints(reader.ReadNumber(), start);
            string mappingText = string.Concat(mapping.Select(char.ConvertFromUtf32));
            for (int codePoint = start; codePoint <= end; codePoint++)
            {
                mappings.Add(codePoint, mappingText);
            }

            start = end + 1;
        }

        return mappings;
    }

    private static Dictionary<int, int> ReadCombiningClasses()
    {
        var classes = new Dictionary<int, int>();
        var reader = new TableReader(CurlUrlHostNameMappingTable.CombiningClasses);
        int start = 0;
        while (!reader.IsAtEnd)
        {
            start += reader.ReadNumber();
            int end = start + reader.ReadNumber();
            int combiningClass = reader.ReadNumber();
            for (int codePoint = start; codePoint <= end; codePoint++)
            {
                classes.Add(codePoint, combiningClass);
            }

            start = end + 1;
        }

        return classes;
    }

    private static Dictionary<long, int> ReadDecompositions()
    {
        var compositions = new Dictionary<long, int>();
        var reader = new TableReader(CurlUrlHostNameMappingTable.Decompositions);
        int codePoint = -1;
        while (!reader.IsAtEnd)
        {
            codePoint += reader.ReadNumber() + 1;
            int header = reader.ReadNumber();
            int[] parts = reader.ReadCodePoints(header / 2, codePoint);
            Decompositions.Add(codePoint, parts);
            if (header % 2 == 1)
            {
                compositions.Add(CompositionKey(parts[0], parts[1]), codePoint);
            }
        }

        return compositions;
    }

    /// <summary>Reads the unsigned LEB128 numbers of one Base64 table in <see cref="CurlUrlHostNameMappingTable" />.</summary>
    private sealed class TableReader(string base64)
    {
        private readonly byte[] bytes = Convert.FromBase64String(base64);

        private int position;

        public bool IsAtEnd => position == bytes.Length;

        public int ReadNumber()
        {
            int value = 0;
            int shift = 0;
            byte current;
            do
            {
                current = bytes[position++];
                value |= (current & 0x7F) << shift;
                shift += 7;
            }
            while (current >= 0x80);

            return value;
        }

        public int[] ReadCodePoints(int count, int previous)
        {
            int[] codePoints = new int[count];
            for (int index = 0; index < count; index++)
            {
                int difference = ReadNumber();
                previous += (difference >> 1) ^ -(difference & 1);
                codePoints[index] = previous;
            }

            return codePoints;
        }
    }
}
