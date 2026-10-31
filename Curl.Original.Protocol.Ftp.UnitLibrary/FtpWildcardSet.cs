using System.Text;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// One <c>[...]</c> set of an FTP wildcard pattern, read as upstream curl 8.22.0's
/// <c>setcharset</c> reads it: bytes and ranges such as <c>a-z</c> (within one of digits, upper
/// or lower case letters), a leading <c>!</c> or <c>^</c> to negate, <c>[:alpha:]</c>-style
/// classes, backslash escapes, and a <c>]</c> first in the set taken as a member.
/// </summary>
internal sealed class FtpWildcardSet
{
    // The classes in the order curl's loop() tests them; only the first one named counts.
    private static readonly (string Name, Func<byte, bool> Contains)[] Classes =
    [
        ("alnum", c => char.IsAsciiLetterOrDigit((char)c)),
        ("alpha", c => char.IsAsciiLetter((char)c)),
        ("digit", c => char.IsAsciiDigit((char)c)),
        ("xdigit", c => char.IsAsciiHexDigit((char)c)),
        ("print", c => c is >= 0x20 and <= 0x7E),
        ("space", IsBlank),
        ("upper", c => char.IsAsciiLetterUpper((char)c)),
        ("lower", c => char.IsAsciiLetterLower((char)c)),
        ("blank", IsBlank),
        ("graph", c => c is >= 0x21 and <= 0x7E),
    ];

    private readonly bool[] members = new bool[256];
    private int classIndex = -1;
    private bool negated;
    private bool somethingFound;
    private SetState state;

    private FtpWildcardSet()
    {
    }

    private enum SetState
    {
        Default,
        AfterFirstRightBracket,
        AfterRightThenLeftBracket,
    }

    /// <summary>Reads the set whose first byte, just past its <c>[</c>, is at <paramref name="start" />.</summary>
    /// <param name="p">The pattern, ending at a NUL.</param>
    /// <param name="start">Where the set's content starts.</param>
    /// <param name="end">Where the set's closing <c>]</c> is.</param>
    /// <returns>The set, or <see langword="null" /> when the pattern ends before the set is closed.</returns>
    public static FtpWildcardSet? Read(byte[] p, int start, out int end)
    {
        FtpWildcardSet set = new();
        for (end = start; p[end] != 0;)
        {
            bool? closed = set.ReadNext(p, ref end);
            if (closed is not null)
            {
                return closed.Value ? set : null;
            }
        }

        return null;
    }

    /// <summary>Whether <paramref name="c" /> is in the set, negation applied.</summary>
    /// <param name="c">The byte.</param>
    /// <returns><see langword="true" /> when the byte matches the set.</returns>
    public bool Contains(byte c)
    {
        bool found = members[c] || (classIndex >= 0 && Classes[classIndex].Contains(c));
        return found != negated;
    }

    private static bool IsBlank(byte c) => c is (byte)' ' or (byte)'\t';

    // Reads one element; true when the set closes here, false when it is malformed, null to go on.
    private bool? ReadNext(byte[] p, ref int i)
    {
        if (state == SetState.Default)
        {
            return ReadDefault(p, ref i);
        }

        byte c = p[i];
        if (c == ']')
        {
            return true;
        }

        if (!TryReadAfterRightBracket(c))
        {
            return false;
        }

        members[c] = true;
        i++;
        return null;
    }

    // After the set's first ], a [ is a member and so is any printable byte; any other byte makes the set malformed.
    private bool TryReadAfterRightBracket(byte c)
    {
        if (state == SetState.AfterRightThenLeftBracket)
        {
            state = SetState.Default;
            return true;
        }

        if (c == '[')
        {
            state = SetState.AfterRightThenLeftBracket;
            return true;
        }

        state = SetState.Default;
        return c is >= 0x20 and <= 0x7E;
    }

    private bool? ReadDefault(byte[] p, ref int i)
    {
        byte c = p[i];
        if (c == ']')
        {
            return ReadRightBracket(ref i);
        }

        if (c is (byte)'^' or (byte)'!')
        {
            ReadNegation(c);
            i++;
            return null;
        }

        if (c == '[')
        {
            ReadLeftBracket(p, ref i);
        }
        else
        {
            ReadMember(p, ref i);
        }

        somethingFound = true;
        return null;
    }

    private bool? ReadRightBracket(ref int i)
    {
        if (somethingFound)
        {
            return true;
        }

        somethingFound = true;
        state = SetState.AfterFirstRightBracket;
        members[']'] = true;
        i++;
        return null;
    }

    // The first ! or ^ negates the set, a second one right after is a member, and any later one too.
    private void ReadNegation(byte c)
    {
        if (somethingFound || negated)
        {
            members[c] = true;
            somethingFound = true;
            return;
        }

        negated = true;
    }

    // [:name:] names a class; any other [ is a member.
    private void ReadLeftBracket(byte[] p, ref int i)
    {
        int after = i + 1;
        if (p[after] == ':' && TryReadClass(p, ref after))
        {
            i = after;
            return;
        }

        members['['] = true;
        i++;
    }

    // parsekeyword: lower case letters, then ":]", naming one of the classes.
    private bool TryReadClass(byte[] p, ref int i)
    {
        int start = i + 1;
        int colon = Array.FindIndex(p, start, b => !char.IsAsciiLetterLower((char)b));
        int index = ClassIndex(p, start, colon);
        if (index < 0)
        {
            return false;
        }

        // As unsigned, "no class yet" (-1) is the largest, so the minimum keeps the class curl tests first.
        classIndex = (int)Math.Min((uint)classIndex, (uint)index);
        i = colon + 2;
        return true;
    }

    // The class the letters from start to colon name when ":]" follows them; -1 otherwise. curl's keyword buffer holds 8 letters.
    private static int ClassIndex(byte[] p, int start, int colon)
    {
        int letters = colon - start;
        if (letters > 8 || p[colon] != ':' || p[colon + 1] != ']')
        {
            return -1;
        }

        string name = Encoding.Latin1.GetString(p, start, letters);
        return Array.FindIndex(Classes, entry => entry.Name == name);
    }

    // A byte, after a backslash when there is one, and the range it starts, as setcharorrange reads it.
    private void ReadMember(byte[] p, ref int i)
    {
        if (ReadsLoneBackslash(p, ref i))
        {
            members['\\'] = true;
            return;
        }

        byte first = p[i++];
        members[first] = true;
        if (char.IsAsciiLetterOrDigit((char)first) && p[i] == '-')
        {
            ReadRange(p, ref i, first);
        }
    }

    // Steps past a backslash; true when nothing follows it.
    private static bool ReadsLoneBackslash(byte[] p, ref int i)
    {
        if (p[i] != '\\')
        {
            return false;
        }

        i++;
        return p[i] == 0;
    }

    // At the '-' after a letter or digit: a range when its end is of the same kind and not below it.
    private void ReadRange(byte[] p, ref int i, byte first)
    {
        int next = i + 1;
        byte last = p[next++];
        if (last == '\\')
        {
            last = p[next++];
        }

        if (last >= first && CharacterClass(last) == CharacterClass(first))
        {
            Array.Fill(members, true, first, last - first + 1);
            i = next;
        }
    }

    private static int CharacterClass(byte c) =>
        char.IsAsciiLetterUpper((char)c) ? 1 : char.IsAsciiLetterLower((char)c) ? 2 : char.IsAsciiDigit((char)c) ? 3 : 0;
}
