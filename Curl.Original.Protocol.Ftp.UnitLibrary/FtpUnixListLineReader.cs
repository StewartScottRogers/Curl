namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// Reads one entry of a Unix <c>ls -l</c> style <c>LIST</c> reply, such as
/// <c>lrwxr-xr-x 1 user01 ftp  512 Jan 29 23:32 prog -&gt; prog2000</c>, as upstream curl
/// 8.22.0's <c>parse_unix</c> does: an optional <c>total</c> line first, then per line the file
/// type, nine permission characters, hard links, user, group, size, a three-part time and the
/// file name, split at the first <c> -&gt; </c> for a symbolic link.
/// </summary>
internal sealed class FtpUnixListLineReader : FtpListLineReader
{
    private const string FileTypeLetters = "-dlpscbD";
    private const string PermissionBytes = "rwx-tTsS\0";
    private const int PermissionLength = 9;
    private const int LastTimeStep = 5;
    private const int LinkArrowLength = 4;
    private const int LinkTargetStep = 6;
    private const string LinkArrow = " -> ";

    private static readonly FtpFileType[] FileTypes =
    [
        FtpFileType.File, FtpFileType.Directory, FtpFileType.SymbolicLink, FtpFileType.NamedPipe,
        FtpFileType.Socket, FtpFileType.DeviceChar, FtpFileType.DeviceBlock, FtpFileType.Door,
    ];

    // Each permission position's characters and the bits each one sets, as ftp_pl_get_permission reads them.
    private static readonly (string Characters, int[] Bits)[] PermissionPositions =
    [
        ("-r", [0, 0x100]), ("-w", [0, 0x80]), ("-xsS", [0, 0x40, 0x840, 0x800]),
        ("-r", [0, 0x20]), ("-w", [0, 0x10]), ("-xsS", [0, 0x8, 0x408, 0x400]),
        ("-r", [0, 0x4]), ("-w", [0, 0x2]), ("-xtT", [0, 0x1, 0x201, 0x200]),
    ];

    private readonly long hardLinkLimit;
    private readonly Func<byte, FtpListStep>[] fieldReaders;
    private UnixField field = UnixField.TotalSize;
    private FtpFileType fileType;
    private int permissions;
    private string permissionsText = string.Empty;
    private long? hardLinks;
    private string user = string.Empty;
    private string group = string.Empty;
    private long? size;
    private string timeText = string.Empty;
    private string fileName = string.Empty;
    private string? target;

    /// <summary>Creates a reader at the start of the reply.</summary>
    /// <param name="hardLinkLimit">
    /// The largest hard link count curl keeps: a C <c>long</c>'s maximum, 2³¹ − 1 on Windows and
    /// 2⁶³ − 1 elsewhere; a larger count is left unknown.
    /// </param>
    public FtpUnixListLineReader(long hardLinkLimit)
    {
        this.hardLinkLimit = hardLinkLimit;
        fieldReaders = [ReadTotalSize, ReadFileType, ReadPermission, ReadHardLinks, ReadUser, ReadGroup, ReadSize, ReadTime, ReadFileName, ReadSymbolicLink];
    }

    private enum UnixField
    {
        TotalSize,
        FileType,
        Permission,
        HardLinks,
        User,
        Group,
        Size,
        Time,
        FileName,
        SymbolicLink,
    }

    /// <inheritdoc />
    public override void StartEntry()
    {
        base.StartEntry();
        hardLinks = null;
        size = null;
        target = null;
    }

    /// <inheritdoc />
    public override FtpListStep Read(byte c)
    {
        return fieldReaders[(int)field](c);
    }

    /// <inheritdoc />
    public override FtpFileListEntry CompletedEntry() => new()
    {
        FileName = fileName,
        FileType = fileType,
        TimeText = timeText,
        Permissions = permissions,
        PermissionsText = permissionsText,
        HardLinks = hardLinks,
        User = user,
        Group = group,
        Size = size,
        Target = target,
    };

    private void NextField(UnixField next)
    {
        field = next;
        Step = 0;
        ItemOffset = 0;
        ItemLength = 0;
    }

    private FtpListStep ReadTotalSize(byte c)
    {
        if (Step == 1)
        {
            return ReadTotalLine(c);
        }

        if (c != 't')
        {
            field = UnixField.FileType;
            return ReadFileType(c);
        }

        Step = 1;
        return FtpListStep.Continue;
    }

    private FtpListStep ReadTotalLine(byte c)
    {
        if (c == '\r')
        {
            Line.RemoveAt(Line.Count - 1);
            return FtpListStep.Continue;
        }

        if (c != '\n')
        {
            return FtpListStep.Continue;
        }

        if (!IsTotalLine(Text(0, Line.Count - 1)))
        {
            return FtpListStep.Malformed;
        }

        field = UnixField.FileType;
        Line.Clear();
        return FtpListStep.Continue;
    }

    // "total ", then any blanks, then digits and nothing more, as parse_unix_totalsize checks.
    private static bool IsTotalLine(string text) =>
        text.StartsWith("total ", StringComparison.Ordinal)
        && text[6..].TrimStart(' ', '\t').All(char.IsAsciiDigit);

    private FtpListStep ReadFileType(byte c)
    {
        int index = FileTypeLetters.IndexOf((char)c, StringComparison.Ordinal);
        if (index < 0)
        {
            return FtpListStep.Malformed;
        }

        fileType = FileTypes[index];
        field = UnixField.Permission;
        ItemLength = 0;
        ItemOffset = 1;
        return FtpListStep.Continue;
    }

    private FtpListStep ReadPermission(byte c)
    {
        ItemLength++;
        if (ItemLength <= PermissionLength)
        {
            return PermissionBytes.Contains((char)c, StringComparison.Ordinal) ? FtpListStep.Continue : FtpListStep.Malformed;
        }

        int? bits = c == ' ' ? PermissionBits() : null;
        if (bits is null)
        {
            return FtpListStep.Malformed;
        }

        permissions = bits.Value;
        permissionsText = Text(ItemOffset, PermissionLength);
        NextField(UnixField.HardLinks);
        return FtpListStep.Continue;
    }

    private int? PermissionBits()
    {
        int bits = 0;
        for (int position = 0; position < PermissionLength; position++)
        {
            (string characters, int[] values) = PermissionPositions[position];
            int index = characters.IndexOf((char)Line[ItemOffset + position], StringComparison.Ordinal);
            if (index < 0)
            {
                return null;
            }

            bits |= values[index];
        }

        return bits;
    }

    private FtpListStep ReadHardLinks(byte c)
    {
        FtpListStep result = ReadNumberWord(c, out string? digits);
        if (digits is not null)
        {
            hardLinks = LeadingNumber(digits, hardLinkLimit);
            NextField(UnixField.User);
        }

        return result;
    }

    private FtpListStep ReadSize(byte c)
    {
        FtpListStep result = ReadNumberWord(c, out string? digits);
        long? number = digits is null ? null : LeadingNumber(digits, long.MaxValue);

        // A size past curl_off_t's range keeps curl reading the size, so the time that follows is malformed.
        if (number is not null)
        {
            size = number == long.MaxValue ? null : number;
            NextField(UnixField.Time);
        }

        return result;
    }

    // A word of digits after any spaces, as the hard link and size states read one.
    private FtpListStep ReadNumberWord(byte c, out string? digits)
    {
        digits = null;
        if (Step == 0)
        {
            return BeginItemIf(c, IsDigit(c));
        }

        ItemLength++;
        if (c == ' ')
        {
            digits = Text(ItemOffset, ItemLength - 1);
            return FtpListStep.Continue;
        }

        return IsDigit(c) ? FtpListStep.Continue : FtpListStep.Malformed;
    }

    // Before an item, spaces are skipped and its first byte must pass the item's own check.
    private FtpListStep BeginItemIf(byte c, bool startsItem)
    {
        if (c == ' ')
        {
            return FtpListStep.Continue;
        }

        if (!startsItem)
        {
            return FtpListStep.Malformed;
        }

        BeginItem();
        return FtpListStep.Continue;
    }

    private FtpListStep ReadUser(byte c)
    {
        string? word = ReadWord(c);
        if (word is not null)
        {
            user = word;
            NextField(UnixField.Group);
        }

        return FtpListStep.Continue;
    }

    private FtpListStep ReadGroup(byte c)
    {
        string? word = ReadWord(c);
        if (word is not null)
        {
            group = word;
            NextField(UnixField.Size);
        }

        return FtpListStep.Continue;
    }

    // Any bytes after any spaces, up to the next space, as the user and group states read them.
    private string? ReadWord(byte c)
    {
        if (Step == 0)
        {
            BeginItemIf(c, startsItem: true);
            return null;
        }

        ItemLength++;
        return c == ' ' ? Text(ItemOffset, ItemLength - 1) : null;
    }

    private FtpListStep ReadTime(byte c)
    {
        if (Step == 0)
        {
            return BeginItemIf(c, char.IsAsciiLetterOrDigit((char)c));
        }

        ItemLength++;
        return Step % 2 == 0 ? ReadTimeGap(c) : ReadTimePart(c);
    }

    // The spaces between two parts of the time; the next part starts with a letter or digit.
    private FtpListStep ReadTimeGap(byte c)
    {
        if (c == ' ')
        {
            return FtpListStep.Continue;
        }

        if (!char.IsAsciiLetterOrDigit((char)c))
        {
            return FtpListStep.Malformed;
        }

        Step++;
        return FtpListStep.Continue;
    }

    // One part of the time: letters, digits and dots, and colons in the last part only.
    private FtpListStep ReadTimePart(byte c)
    {
        if (c == ' ')
        {
            EndTimePart();
            return FtpListStep.Continue;
        }

        bool allowed = char.IsAsciiLetterOrDigit((char)c) || c == '.' || (c == ':' && Step == LastTimeStep);
        return allowed ? FtpListStep.Continue : FtpListStep.Malformed;
    }

    private void EndTimePart()
    {
        if (Step < LastTimeStep)
        {
            Step++;
            return;
        }

        timeText = Text(ItemOffset, ItemLength - 1);
        field = fileType == FtpFileType.SymbolicLink ? UnixField.SymbolicLink : UnixField.FileName;
        Step = 0;
    }

    private FtpListStep ReadFileName(byte c)
    {
        if (Step == 0)
        {
            return BeginItemIf(c, startsItem: true);
        }

        FtpListStep result = ReadToLineEnd(c, Step == 2, out string? name);
        if (name is not null)
        {
            fileName = name;
            field = UnixField.FileType;
        }

        return result;
    }

    private FtpListStep ReadSymbolicLink(byte c)
    {
        if (Step == 0)
        {
            return BeginItemIf(c, startsItem: true);
        }

        if (Step < LinkTargetStep - 1)
        {
            return ReadLinkName(c);
        }

        return Step == LinkTargetStep - 1 ? BeginLinkTarget(c) : ReadLinkTarget(c);
    }

    // Steps 1 to 4 read the name and match " -> " a byte at a time; a byte that breaks the arrow returns to step 1.
    private FtpListStep ReadLinkName(byte c)
    {
        ItemLength++;
        if (c is (byte)'\r' or (byte)'\n')
        {
            return FtpListStep.Malformed;
        }

        if (c != LinkArrow[Step - 1])
        {
            Step = 1;
            return FtpListStep.Continue;
        }

        Step++;
        if (Step == LinkTargetStep - 1)
        {
            fileName = Text(ItemOffset, ItemLength - LinkArrowLength);
            ItemOffset = 0;
            ItemLength = 0;
        }

        return FtpListStep.Continue;
    }

    private FtpListStep BeginLinkTarget(byte c)
    {
        if (c is (byte)'\r' or (byte)'\n')
        {
            return FtpListStep.Malformed;
        }

        BeginItem();
        Step = LinkTargetStep;
        return FtpListStep.Continue;
    }

    private FtpListStep ReadLinkTarget(byte c)
    {
        FtpListStep result = ReadToLineEnd(c, Step == LinkTargetStep + 1, out string? linkTarget);
        if (linkTarget is not null)
        {
            target = linkTarget;
            field = UnixField.FileType;
        }

        return result;
    }
}
