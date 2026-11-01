namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// Reads one entry of a Windows (DOS) style <c>LIST</c> reply, such as
/// <c>01-29-97  11:32PM       &lt;DIR&gt;          prog</c>, as upstream curl 8.22.0's
/// <c>parse_winnt</c> does: an eight-byte date of digits and dashes, a time, <c>&lt;DIR&gt;</c>
/// or a size, and a file name to the end of the line.
/// </summary>
internal sealed class FtpDosListLineReader : FtpListLineReader
{
    private const int DateLength = 8;
    private const string DateBytes = "0123456789-\0";
    private const string TimeBytes = "APM0123456789:\0";
    private const string DirectoryMark = "<DIR>";

    private readonly Func<byte, FtpListStep>[] fieldReaders;
    private DosField field = DosField.Date;
    private string timeText = string.Empty;
    private FtpFileType fileType;
    private long size;
    private string fileName = string.Empty;

    /// <summary>Creates a reader at the start of the reply.</summary>
    public FtpDosListLineReader()
    {
        fieldReaders = [ReadDate, ReadTime, ReadDirectoryOrSize, ReadFileName];
    }

    private enum DosField
    {
        Date,
        Time,
        DirectoryOrSize,
        FileName,
    }

    /// <inheritdoc />
    public override FtpListStep Read(byte c) => fieldReaders[(int)field](c);

    /// <inheritdoc />
    public override FtpFileListEntry CompletedEntry() => new()
    {
        FileName = fileName,
        FileType = fileType,
        TimeText = timeText,
        Size = size,
    };

    private FtpListStep ReadDate(byte c)
    {
        ItemLength++;
        if (ItemLength <= DateLength)
        {
            return DateBytes.Contains((char)c, StringComparison.Ordinal) ? FtpListStep.Continue : FtpListStep.Malformed;
        }

        if (c != ' ')
        {
            return FtpListStep.Malformed;
        }

        field = DosField.Time;
        Step = 0;
        return FtpListStep.Continue;
    }

    private FtpListStep ReadTime(byte c)
    {
        ItemLength++;
        if (Step == 0)
        {
            // Blanks before the time are skipped; its first byte is taken unchecked.
            Step = IsBlank(c) ? 0 : 1;
            return FtpListStep.Continue;
        }

        if (c == ' ')
        {
            EndTime();
            return FtpListStep.Continue;
        }

        return TimeBytes.Contains((char)c, StringComparison.Ordinal) ? FtpListStep.Continue : FtpListStep.Malformed;
    }

    private static bool IsBlank(byte c) => c == ' ' || c == '\t';

    // The time text runs from the start of the line, so it carries the date too.
    private void EndTime()
    {
        timeText = Text(ItemOffset, ItemLength - 1);
        field = DosField.DirectoryOrSize;
        Step = 0;
        ItemLength = 0;
    }

    private FtpListStep ReadDirectoryOrSize(byte c)
    {
        if (Step == 0)
        {
            BeginItemUnlessSpace(c);
            return FtpListStep.Continue;
        }

        ItemLength++;
        if (c != ' ')
        {
            return FtpListStep.Continue;
        }

        string word = Text(ItemOffset, ItemLength - 1);
        if (!TrySetDirectoryOrSize(word))
        {
            return FtpListStep.Malformed;
        }

        field = DosField.FileName;
        Step = 0;
        ItemLength = 0;
        return FtpListStep.Continue;
    }

    private bool TrySetDirectoryOrSize(string word)
    {
        if (word == DirectoryMark)
        {
            fileType = FtpFileType.Directory;
            size = 0;
            return true;
        }

        long? number = LeadingNumber(word.TrimStart(' ', '\t'), long.MaxValue);
        fileType = FtpFileType.File;
        size = number.GetValueOrDefault();
        return number is not null;
    }

    private FtpListStep ReadFileName(byte c)
    {
        if (Step == 0)
        {
            BeginItemUnlessSpace(c);
            return FtpListStep.Continue;
        }

        FtpListStep result = ReadToLineEnd(c, Step == 2, out string? name);
        if (name is not null)
        {
            fileName = name;
            field = DosField.Date;
        }

        return result;
    }

    private void BeginItemUnlessSpace(byte c)
    {
        if (c != ' ')
        {
            BeginItem();
        }
    }
}
