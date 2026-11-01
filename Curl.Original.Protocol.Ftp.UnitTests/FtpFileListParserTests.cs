using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// Pins <see cref="FtpFileListParser" /> on upstream curl 8.22.0's simulated FTP listings
/// (<c>tests/directories.pm</c>, <c>/fully_simulated/UNIX/</c> and <c>/fully_simulated/DOS/</c>,
/// whose parsed fields test576 prints) and on each way <c>lib/ftplistparser.c</c> finds a
/// listing malformed (BL-2476).
/// </summary>
[TestClass]
public sealed class FtpFileListParserTests
{
    private const string UnixListing =
        "drwxrwxrwx    4 ftp-default ftp-default   20480 Apr 27  5:12 .\r\n"
        + "drwxrwxrwx    4 ftp-default ftp-default   20480 Apr 23  3:12 ..\r\n"
        + "-r--r--r--    1 ftp-default ftp-default      38 Jan 11 10:00 chmod1\r\n"
        + "-rw-rw-rw-    1 ftp-default ftp-default      38 Feb  1  8:00 chmod2\r\n"
        + "-rwxrwxrwx    1 ftp-default ftp-default      38 Feb  1  8:00 chmod3\r\n"
        + "d--S--S--t    1 ftp-default ftp-default    4096 May  4  4:31 chmod4\r\n"
        + "d--s--s--T    1 ftp-default ftp-default    4096 May  4  4:31 chmod5\r\n"
        + "-rw-r--r--    1 ftp-default ftp-default       0 Apr 27 11:01 empty_file.dat\r\n"
        + "-rw-r--r--    1 ftp-default ftp-default      35 Apr 27 11:01 file.txt\r\n"
        + "lrwxrwxrwx    1 ftp-default ftp-default       0 Jan  6  4:42 link -> file.txt\r\n"
        + "lrwxrwxrwx    1 ftp-default ftp-default       0 Jan  6  4:45 link_absolute -> /data/ftp/file.txt\r\n"
        + "drwxrwxrwx    4 ftp-default ftp-default    4096 Jan 23  2:05 .NeXT\r\n"
        + "-rw-r--r--    1 ftp-default ftp-default      47 Apr 27 11:01 someothertext.txt\r\n"
        + "drwxr-xrwx    2 ftp-default ftp-default    4096 Apr 23  3:12 weirddir.txt\r\n";

    private const string DosListing =
        "04-27-10  05:12AM       <DIR>          .\r\n"
        + "04-23-10  03:12AM       <DIR>          ..\r\n"
        + "01-11-10  10:00AM                   38 chmod1\r\n"
        + "02-01-10  08:00AM                   38 chmod2\r\n"
        + "02-01-10  08:00AM                   38 chmod3\r\n"
        + "05-04-10  04:31AM       <DIR>          chmod4\r\n"
        + "05-04-10  04:31AM       <DIR>          chmod5\r\n"
        + "04-27-10  11:01AM                    0 empty_file.dat\r\n"
        + "04-27-10  11:01AM                   35 file.txt\r\n"
        + "01-23-05  02:05AM       <DIR>          .NeXT\r\n"
        + "04-27-10  11:01AM                   47 someothertext.txt\r\n"
        + "04-23-10  03:12AM       <DIR>          weirddir.txt\r\n";

    [TestMethod]
    public void Read_UpstreamUnixListing_ReadsEveryFieldTest576Prints()
    {
        FtpFileListParser parser = Parse(UnixListing);

        Assert.IsNull(parser.Failure);
        string[] expected =
        [
            "Directory . rwxrwxrwx 777 20480 Apr 27  5:12 -", "Directory .. rwxrwxrwx 777 20480 Apr 23  3:12 -",
            "File chmod1 r--r--r-- 444 38 Jan 11 10:00 -", "File chmod2 rw-rw-rw- 666 38 Feb  1  8:00 -",
            "File chmod3 rwxrwxrwx 777 38 Feb  1  8:00 -", "Directory chmod4 --S--S--t 7001 4096 May  4  4:31 -",
            "Directory chmod5 --s--s--T 7110 4096 May  4  4:31 -", "File empty_file.dat rw-r--r-- 644 0 Apr 27 11:01 -",
            "File file.txt rw-r--r-- 644 35 Apr 27 11:01 -", "SymbolicLink link rwxrwxrwx 777 0 Jan  6  4:42 file.txt",
            "SymbolicLink link_absolute rwxrwxrwx 777 0 Jan  6  4:45 /data/ftp/file.txt",
            "Directory .NeXT rwxrwxrwx 777 4096 Jan 23  2:05 -", "File someothertext.txt rw-r--r-- 644 47 Apr 27 11:01 -",
            "Directory weirddir.txt rwxr-xrwx 757 4096 Apr 23  3:12 -",
        ];
        CollectionAssert.AreEqual(expected, parser.Entries.Select(Describe).ToArray());
        Assert.IsTrue(parser.Entries.All(e => e.User == "ftp-default" && e.Group == "ftp-default"));
        CollectionAssert.AreEqual(new long?[] { 4, 4, 1, 1, 1, 1, 1, 1, 1, 1, 1, 4, 1, 2 }, parser.Entries.Select(e => e.HardLinks).ToArray());
    }

    [TestMethod]
    public void Read_UpstreamDosListing_ReadsTypeSizeTimeAndName()
    {
        FtpFileListParser parser = Parse(DosListing);

        Assert.IsNull(parser.Failure);
        Assert.HasCount(12, parser.Entries);
        FtpFileListEntry dot = parser.Entries[0];
        Assert.AreEqual(new FtpFileListEntry { FileName = ".", FileType = FtpFileType.Directory, TimeText = "04-27-10  05:12AM", Size = 0 }, dot);
        FtpFileListEntry file = parser.Entries[8];
        Assert.AreEqual(new FtpFileListEntry { FileName = "file.txt", FileType = FtpFileType.File, TimeText = "04-27-10  11:01AM", Size = 35 }, file);
        CollectionAssert.AreEqual(
            new[] { ".", "..", "chmod1", "chmod2", "chmod3", "chmod4", "chmod5", "empty_file.dat", "file.txt", ".NeXT", "someothertext.txt", "weirddir.txt" },
            parser.Entries.Select(e => e.FileName).ToArray());
    }

    [TestMethod]
    public void Read_ListingFedOneByteAtATime_ReadsTheSameEntries()
    {
        FtpFileListParser parser = new(_ => true, runsOnWindows: true);

        foreach (byte b in Encoding.Latin1.GetBytes(UnixListing))
        {
            parser.Read([b]);
        }

        CollectionAssert.AreEqual(Parse(UnixListing).Entries.ToArray(), parser.Entries.ToArray());
    }

    [TestMethod]
    public void Read_SelectorRejectsAName_KeepsOnlyTheOthersAndAsksForEveryEntryInOrder()
    {
        List<string> asked = [];

        FtpFileListParser parser = Parse(DosListing, name =>
        {
            asked.Add(name);
            return FtpWildcardPattern.Matches("*.txt", name);
        });

        CollectionAssert.AreEqual(new[] { "file.txt", "someothertext.txt", "weirddir.txt" }, parser.Entries.Select(e => e.FileName).ToArray());
        Assert.HasCount(12, asked);
        Assert.AreEqual(".", asked[0]);
    }

    [TestMethod]
    [DataRow("total 20\r\n")]
    [DataRow("total \t 20\n")]
    [DataRow("total \n")]
    [DataRow("total 2\0x\n")]
    public void Read_TotalLineFirst_IsSkipped(string totalLine)
    {
        FtpFileListParser parser = Parse(totalLine + "-rw-r--r-- 1 u g 5 Jan  1  2020 a\n");

        Assert.IsNull(parser.Failure);
        Assert.AreEqual("a", parser.Entries.Single().FileName);
    }

    [TestMethod]
    [DataRow("totally 20\n")]
    [DataRow("total 20x\n")]
    [DataRow("xrw-r--r-- 1 u g 5 Jan  1  2020 a\n")]
    [DataRow("-rw-q--r-- 1 u g 5 Jan  1  2020 a\n")]
    [DataRow("-rw-r--r--x 1 u g 5 Jan  1  2020 a\n")]
    [DataRow("-wr-r--r-- 1 u g 5 Jan  1  2020 a\n")]
    [DataRow("-rw-r-\0r-- 1 u g 5 Jan  1  2020 a\n")]
    [DataRow("-rw-r--r-- x u g 5 Jan  1  2020 a\n")]
    [DataRow("-rw-r--r-- 1x u g 5 Jan  1  2020 a\n")]
    [DataRow("-rw-r--r-- 1 u g x Jan  1  2020 a\n")]
    [DataRow("-rw-r--r-- 1 u g 5x Jan  1  2020 a\n")]
    [DataRow("-rw-r--r-- 1 u g 5 -an  1  2020 a\n")]
    [DataRow("-rw-r--r-- 1 u g 5 J:n  1  2020 a\n")]
    [DataRow("-rw-r--r-- 1 u g 5 Jan  -  2020 a\n")]
    [DataRow("-rw-r--r-- 1 u g 5 Jan  1  -020 a\n")]
    [DataRow("-rw-r--r-- 1 u g 5 Jan  1  20-0 a\n")]
    [DataRow("-rw-r--r-- 1 u g 5 Jan  1  2020 a\rb\n")]
    [DataRow("crw-rw-rw- 1 root root 1,   3 Jan  1  2020 null\n")]
    public void Read_MalformedUnixLine_FailsWithBadFileList(string listing)
    {
        FtpFileListParser parser = Parse(listing);

        Assert.AreEqual(CurlExitCode.FtpBadFileList, parser.Failure);
        Assert.IsEmpty(parser.Entries);
    }

    [TestMethod]
    [DataRow("01-11-1x  10:00AM 38 a\r\n")]
    [DataRow("01-11-100 10:00AM 38 a\r\n")]
    [DataRow("01-11-10  10:00XM 38 a\r\n")]
    [DataRow("01-11-10  10:00AM abc a\r\n")]
    [DataRow("01-11-10  10:00AM \t a\r\n")]
    [DataRow("01-11-10  10:00AM 99999999999999999999 a\r\n")]
    [DataRow("01-11-10  10:00AM 38 a\rb\n")]
    public void Read_MalformedDosLine_FailsWithBadFileList(string listing)
    {
        FtpFileListParser parser = Parse(listing);

        Assert.AreEqual(CurlExitCode.FtpBadFileList, parser.Failure);
        Assert.IsEmpty(parser.Entries);
    }

    [TestMethod]
    public void Read_DosLineEndedByLineFeedWithTabsAndOddTime_IsRead()
    {
        FtpFileListParser parser = Parse("01-11-10 \t1:0P \t12 my file\n");

        Assert.AreEqual(new FtpFileListEntry { FileName = "my file", FileType = FtpFileType.File, TimeText = "01-11-10 \t1:0P", Size = 12 }, parser.Entries.Single());
    }

    [TestMethod]
    public void Read_TotalLineAfterAnEntry_KeepsTheEntriesBeforeItAndIgnoresLaterChunks()
    {
        FtpFileListParser parser = new(_ => true, runsOnWindows: true);

        parser.Read(Encoding.Latin1.GetBytes("-rw-r--r-- 1 u g 5 Jan  1  2020 a\ntotal 20\n"));
        parser.Read(Encoding.Latin1.GetBytes("-rw-r--r-- 1 u g 5 Jan  1  2020 b\n"));

        Assert.AreEqual(CurlExitCode.FtpBadFileList, parser.Failure);
        Assert.AreEqual("a", parser.Entries.Single().FileName);
    }

    [TestMethod]
    [DataRow('b', FtpFileType.DeviceBlock)]
    [DataRow('c', FtpFileType.DeviceChar)]
    [DataRow('p', FtpFileType.NamedPipe)]
    [DataRow('s', FtpFileType.Socket)]
    [DataRow('D', FtpFileType.Door)]
    public void Read_SpecialFileTypeLetter_ReadsItsType(char letter, FtpFileType expected)
    {
        FtpFileListParser parser = Parse(letter + "rw-r----- 1 root disk 0 Jan  1  2020 dev\n");

        Assert.AreEqual(expected, parser.Entries.Single().FileType);
    }

    [TestMethod]
    public void Read_LinkWhoseTargetHoldsAnotherArrow_IsDropped()
    {
        FtpFileListParser parser = Parse(
            "lrwxrwxrwx 1 u g 0 Jan  1  2020 a -> b -> c\n"
            + "lrwxrwxrwx 1 u g 0 Jan  1  2020 x - y -z -> t\r\n");

        FtpFileListEntry link = parser.Entries.Single();
        Assert.AreEqual("x - y -z", link.FileName);
        Assert.AreEqual("t", link.Target);
    }

    [TestMethod]
    [DataRow("lrwxrwxrwx 1 u g 0 Jan  1  2020 a\n")]
    [DataRow("lrwxrwxrwx 1 u g 0 Jan  1  2020 a \n")]
    [DataRow("lrwxrwxrwx 1 u g 0 Jan  1  2020 a -\n")]
    [DataRow("lrwxrwxrwx 1 u g 0 Jan  1  2020 a ->\r")]
    [DataRow("lrwxrwxrwx 1 u g 0 Jan  1  2020 a -> \n")]
    [DataRow("lrwxrwxrwx 1 u g 0 Jan  1  2020 a -> b\rc")]
    public void Read_LinkWithoutTarget_FailsWithBadFileList(string listing)
    {
        FtpFileListParser parser = Parse(listing);

        Assert.AreEqual(CurlExitCode.FtpBadFileList, parser.Failure);
    }

    [TestMethod]
    [DataRow(true, null)]
    [DataRow(false, 2147483648L)]
    public void Read_HardLinkCountPastWindowsLong_IsUnknownOnlyOnWindows(bool runsOnWindows, long? expected)
    {
        FtpFileListParser parser = Parse("-rw-r--r-- 2147483648 u g 5 Jan  1  2020 a\n", runsOnWindows: runsOnWindows);

        Assert.AreEqual(expected, parser.Entries.Single().HardLinks);
    }

    [TestMethod]
    public void Read_SizeOfCurlOffTMaximum_IsUnknown()
    {
        FtpFileListParser parser = Parse("-rw-r--r-- 1 u g 9223372036854775807 Jan  1  2020 a\n");

        Assert.IsNull(parser.Entries.Single().Size);
    }

    [TestMethod]
    public void Read_SizePastCurlOffT_KeepsReadingTheSizeSoTheTimeIsMalformed()
    {
        FtpFileListParser parser = Parse("-rw-r--r-- 1 u g 9223372036854775808 12 Jan  1  2020 a\n");

        Assert.AreEqual(CurlExitCode.FtpBadFileList, parser.Failure);
    }

    [TestMethod]
    public void Read_NulByteInAField_CutsTheFieldThereAsCurlsCStringsDo()
    {
        FtpFileListParser parser = Parse("-rw-r--r-- 1 ab\0cd g 5 Jan  1  2020 na\0me\n");

        FtpFileListEntry entry = parser.Entries.Single();
        Assert.AreEqual("ab", entry.User);
        Assert.AreEqual("na", entry.FileName);
    }

    [TestMethod]
    public void Read_EntryUpToCurlsBufferLimit_IsRead()
    {
        string prefix = "-rw-r--r-- 1 u g 5 Jan  1  2020 ";
        string name = new('n', 9999 - prefix.Length - 1);

        FtpFileListParser parser = Parse(prefix + name + "\n");

        Assert.AreEqual(name, parser.Entries.Single().FileName);
    }

    [TestMethod]
    public void Read_EntryPastCurlsBufferLimit_FailsWithOutOfMemory()
    {
        string prefix = "-rw-r--r-- 1 u g 5 Jan  1  2020 ";

        FtpFileListParser parser = Parse(prefix + new string('n', 9999 - prefix.Length) + "\n");

        Assert.AreEqual(CurlExitCode.OutOfMemory, parser.Failure);
    }

    [TestMethod]
    public void Read_ListingEndingPartWayThroughAnEntry_KeepsOnlyCompletedEntries()
    {
        FtpFileListParser parser = Parse("-rw-r--r-- 1 u g 5 Jan  1  2020 a\n-rw-r--r-- 1 u g 5 Jan");

        Assert.IsNull(parser.Failure);
        Assert.AreEqual("a", parser.Entries.Single().FileName);
    }

    [TestMethod]
    public void Read_EmptyChunkFirst_LeavesTheFormatToTheFirstByte()
    {
        FtpFileListParser parser = new(_ => true, runsOnWindows: true);

        parser.Read([]);
        parser.Read(Encoding.Latin1.GetBytes("01-11-10  10:00AM 38 a\n"));

        Assert.AreEqual(38, parser.Entries.Single().Size);
    }

    [TestMethod]
    public void Constructor_NullSelector_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new FtpFileListParser(null!, runsOnWindows: true));
    }

    private static FtpFileListParser Parse(string listing, Func<string, bool>? selectsFileName = null, bool runsOnWindows = true)
    {
        FtpFileListParser parser = new(selectsFileName ?? (_ => true), runsOnWindows);
        parser.Read(Encoding.Latin1.GetBytes(listing));
        return parser;
    }

    private static string Describe(FtpFileListEntry e) =>
        $"{e.FileType} {e.FileName} {e.PermissionsText} {Convert.ToString(e.Permissions!.Value, 8)} {e.Size} {e.TimeText} {e.Target ?? "-"}";
}
