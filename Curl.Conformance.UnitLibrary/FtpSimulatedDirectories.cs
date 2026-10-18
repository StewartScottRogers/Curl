using System.Text;

namespace Curl.Conformance;

/// <summary>
/// The simulated directories of upstream's <c>tests/directories.pm</c> (at <c>curl-8_22_0</c>), which
/// <c>tests/ftpserver.pl</c> serves once <c>CWD</c> has moved into <c>/fully_simulated</c>: <c>LIST</c>
/// sends the directory's fixed listing (UNIX style under <c>/fully_simulated/UNIX/</c>, DOS style under
/// <c>/fully_simulated/DOS/</c>, nothing for any other directory) and <c>RETR</c> and <c>SIZE</c> answer
/// from the content of the file the listing names (BL-2618).
/// </summary>
internal static class FtpSimulatedDirectories
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

    private static readonly Dictionary<string, string> Listings = new(StringComparer.Ordinal)
    {
        ["/fully_simulated/UNIX/"] = UnixListing,
        ["/fully_simulated/DOS/"] = DosListing,
    };

    // directories.pm gives content only to the regular files; any other name it lists has none.
    private static readonly Dictionary<string, string> FileContents = new(StringComparer.Ordinal)
    {
        ["chmod1"] = "This file should have permissions 444\n",
        ["chmod2"] = "This file should have permissions 666\n",
        ["chmod3"] = "This file should have permissions 777\n",
        ["empty_file.dat"] = string.Empty,
        ["file.txt"] = "This is content of file \"file.txt\"\n",
        ["someothertext.txt"] = "Some junk ;-) This file does not really exist.\n",
    };

    /// <summary>Whether ftpserver.pl serves the directory from these lists: its <c>/^\/fully_simulated/</c>.</summary>
    /// <param name="directory">The current directory, with a trailing slash, as <c>CWD</c> left it.</param>
    /// <returns><see langword="true"/> for <c>/fully_simulated</c> and everything under it.</returns>
    public static bool IsSimulated(string directory) => directory.StartsWith("/fully_simulated", StringComparison.Ordinal);

    /// <summary>The bytes <c>LIST</c> sends for a simulated directory.</summary>
    /// <param name="directory">The current directory, with a trailing slash.</param>
    /// <returns>The directory's listing, CRLF line ends; empty for a directory with no list.</returns>
    public static byte[] Listing(string directory) =>
        Listings.TryGetValue(directory, out string? listing) ? Encoding.Latin1.GetBytes(listing) : [];

    /// <summary>Finds the content of a file in a simulated directory, as directories.pm's <c>wildcard_getfile</c> does.</summary>
    /// <param name="directory">The current directory, with a trailing slash.</param>
    /// <param name="name">The file name <c>RETR</c> or <c>SIZE</c> names.</param>
    /// <returns>The file's bytes; <see langword="null"/> when the directory has no list or the list no such file.</returns>
    public static byte[]? FileContent(string directory, string name) =>
        Listings.ContainsKey(directory) && FileContents.TryGetValue(name, out string? content) ? Encoding.Latin1.GetBytes(content) : null;
}
