// Generates Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHostNameMappingTable.cs from the Unicode 15.1 data (ADR-0980).
// Usage: dotnet run Generate-CurlUrlHostNameMappingTable.cs -- <IdnaMappingTable.txt> <UnicodeData.txt> <CompositionExclusions.txt> [output]
//   The three files are https://www.unicode.org/Public/idna/15.1.0/IdnaMappingTable.txt,
//   https://www.unicode.org/Public/15.1.0/ucd/UnicodeData.txt and
//   https://www.unicode.org/Public/15.1.0/ucd/CompositionExclusions.txt.
// output defaults to Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHostNameMappingTable.cs.
// Each table is Base64 of unsigned LEB128 numbers, read back by CurlUrlHostNameMapping.ReadMappings,
// ReadCombiningClasses and ReadDecompositions.
using System.Globalization;
using System.Text;

if (args.Length < 3)
{
    Console.Error.WriteLine("usage: dotnet run Generate-CurlUrlHostNameMappingTable.cs -- <IdnaMappingTable.txt> <UnicodeData.txt> <CompositionExclusions.txt> [output]");
    return 2;
}

string outputPath = args.Length > 3 ? args[3] : Path.Combine("Curl.Protocol.Abstractions.UnitLibrary", "CurlUrlHostNameMappingTable.cs");
var unicodeData = MappingTableGenerator.ReadUnicodeData(args[1]);
var excluded = MappingTableGenerator.ReadCompositionExclusions(args[2]);

var source = new StringBuilder();
source.Append("namespace Curl.Protocol.Abstractions;\r\n\r\n");
source.Append("/// <summary>\r\n");
source.Append("/// The Unicode 15.1 data <see cref=\"CurlUrlHostNameMapping\" /> maps and normalizes with, generated\r\n");
source.Append("/// from <c>IdnaMappingTable.txt</c>, <c>UnicodeData.txt</c> and <c>CompositionExclusions.txt</c> by a\r\n");
source.Append("/// C# file-based app (ADR-0980). Each table is a Base64 run of unsigned LEB128 numbers; a code point\r\n");
source.Append("/// is the gap after the previous entry, and a mapped or decomposed code point is a zig-zag\r\n");
source.Append("/// difference from the one before it.\r\n");
source.Append("/// </summary>\r\n");
source.Append("internal static class CurlUrlHostNameMappingTable\r\n{\r\n");
MappingTableGenerator.AppendTable(
    source,
    "Mappings",
    ["The UTS #46 mapped, <c>disallowed_STD3_mapped</c> and ignored ranges: gap, range length less", "one, mapping length (0 for ignored) and the mapping's code points."],
    MappingTableGenerator.EncodeMappings(args[0]));
source.Append("\r\n");
MappingTableGenerator.AppendTable(
    source,
    "CombiningClasses",
    ["The canonical combining classes other than 0: gap, range length less one and class."],
    MappingTableGenerator.EncodeCombiningClasses(unicodeData));
source.Append("\r\n");
MappingTableGenerator.AppendTable(
    source,
    "Decompositions",
    ["The canonical decompositions: gap, twice the length plus 1 when the pair composes back", "(not a composition exclusion), and the decomposition's code points."],
    MappingTableGenerator.EncodeDecompositions(unicodeData, excluded));
source.Append("}\r\n");
File.WriteAllText(outputPath, source.ToString(), new UTF8Encoding(false));
return 0;

internal static class MappingTableGenerator
{
    internal static void AppendTable(StringBuilder source, string name, string[] summary, byte[] table)
    {
        const int LineLength = 112;
        source.Append("    /// <summary>\r\n");
        foreach (string line in summary)
        {
            source.Append("    /// ").Append(line).Append("\r\n");
        }

        source.Append("    /// </summary>\r\n");
        source.Append("    public const string ").Append(name).Append(" = \"\"\r\n");
        string base64 = Convert.ToBase64String(table);
        for (int index = 0; index < base64.Length; index += LineLength)
        {
            source.Append("        + \"").Append(base64.AsSpan(index, Math.Min(LineLength, base64.Length - index))).Append("\"\r\n");
        }

        source.Append("        ;\r\n");
    }

    internal static byte[] EncodeMappings(string idnaMappingTablePath)
    {
        var table = new List<byte>();
        int next = 0;
        foreach (string line in File.ReadLines(idnaMappingTablePath))
        {
            string[] fields = line.Split('#')[0].Split(';', StringSplitOptions.TrimEntries);
            if (fields.Length < 2 || fields[1] is not ("mapped" or "disallowed_STD3_mapped" or "ignored"))
            {
                continue;
            }

            string[] range = fields[0].Split("..");
            int start = ParseCodePoint(range[0]);
            int end = ParseCodePoint(range[^1]);
            int[] mapping = fields[1] == "ignored" ? [] : ParseCodePoints(fields[2]);
            AppendNumber(table, start - next);
            AppendNumber(table, end - start);
            AppendNumber(table, mapping.Length);
            AppendCodePoints(table, mapping, start);
            next = end + 1;
        }

        return [.. table];
    }

    internal static byte[] EncodeCombiningClasses(SortedDictionary<int, (int CombiningClass, int[] Decomposition)> unicodeData)
    {
        var table = new List<byte>();
        int next = 0;
        int rangeStart = -1;
        int rangeEnd = -1;
        int rangeClass = 0;
        foreach (var (codePoint, (combiningClass, _)) in unicodeData)
        {
            if (combiningClass == 0)
            {
                continue;
            }

            if (codePoint == rangeEnd + 1 && combiningClass == rangeClass)
            {
                rangeEnd = codePoint;
                continue;
            }

            if (rangeStart >= 0)
            {
                next = AppendCombiningClassRange(table, next, rangeStart, rangeEnd, rangeClass);
            }

            (rangeStart, rangeEnd, rangeClass) = (codePoint, codePoint, combiningClass);
        }

        AppendCombiningClassRange(table, next, rangeStart, rangeEnd, rangeClass);
        return [.. table];
    }

    internal static int AppendCombiningClassRange(List<byte> table, int next, int start, int end, int combiningClass)
    {
        AppendNumber(table, start - next);
        AppendNumber(table, end - start);
        AppendNumber(table, combiningClass);
        return end + 1;
    }

    internal static byte[] EncodeDecompositions(SortedDictionary<int, (int CombiningClass, int[] Decomposition)> unicodeData, HashSet<int> excluded)
    {
        var table = new List<byte>();
        int previous = -1;
        foreach (var (codePoint, (combiningClass, decomposition)) in unicodeData)
        {
            if (decomposition.Length == 0)
            {
                continue;
            }

            bool isPrimaryComposite = decomposition.Length == 2
                && !excluded.Contains(codePoint)
                && combiningClass == 0
                && unicodeData.GetValueOrDefault(decomposition[0]).CombiningClass == 0;
            AppendNumber(table, codePoint - previous - 1);
            AppendNumber(table, (decomposition.Length * 2) + (isPrimaryComposite ? 1 : 0));
            AppendCodePoints(table, decomposition, codePoint);
            previous = codePoint;
        }

        return [.. table];
    }

    internal static SortedDictionary<int, (int CombiningClass, int[] Decomposition)> ReadUnicodeData(string path)
    {
        var unicodeData = new SortedDictionary<int, (int, int[])>();
        foreach (string line in File.ReadLines(path))
        {
            string[] fields = line.Split(';');
            if (fields.Length < 6)
            {
                continue;
            }

            int[] decomposition = fields[5].Length == 0 || fields[5].StartsWith('<') ? [] : ParseCodePoints(fields[5]);
            unicodeData[ParseCodePoint(fields[0])] = (int.Parse(fields[3], CultureInfo.InvariantCulture), decomposition);
        }

        return unicodeData;
    }

    internal static HashSet<int> ReadCompositionExclusions(string path) =>
        [.. File.ReadLines(path).Select(line => line.Split('#')[0].Trim()).Where(field => field.Length > 0).Select(ParseCodePoint)];

    internal static int ParseCodePoint(string text) => int.Parse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    internal static int[] ParseCodePoints(string text) =>
        [.. text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(ParseCodePoint)];

    internal static void AppendCodePoints(List<byte> table, int[] codePoints, int previous)
    {
        foreach (int codePoint in codePoints)
        {
            int difference = codePoint - previous;
            AppendNumber(table, (difference << 1) ^ (difference >> 31));
            previous = codePoint;
        }
    }

    internal static void AppendNumber(List<byte> table, int value)
    {
        uint remaining = (uint)value;
        while (remaining >= 0x80)
        {
            table.Add((byte)(remaining | 0x80));
            remaining >>= 7;
        }

        table.Add((byte)remaining);
    }
}
