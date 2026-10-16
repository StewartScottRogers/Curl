using Curl.Cli;

namespace Curl.Mcp;

/// <summary>
/// Tests for <see cref="McpOptionPathTable" />. Each data row cites, in a comment, the sentence of
/// the curl 8.22.0 manpage (https://curl.se/docs/manpage.html) its classification rests on.
/// </summary>
[TestClass]
public sealed class McpOptionPathTableTests
{
    [TestMethod]
    public void Classifications_EveryOptionInCommandLineOptionTable_IsClassified()
    {
        var unclassified = CommandLineOptionTable.Rows
            .Select(row => row.LongName)
            .Where(longName => !McpOptionPathTable.Classifications.ContainsKey(longName))
            .ToList();

        Assert.IsEmpty(unclassified, $"Options with no path classification: --{string.Join(", --", unclassified)}");
    }

    [TestMethod]
    public void Classifications_EveryClassifiedOption_IsInCommandLineOptionTable()
    {
        var tableNames = CommandLineOptionTable.Rows.Select(row => row.LongName).ToHashSet(StringComparer.Ordinal);

        var stale = McpOptionPathTable.Classifications.Keys.Where(name => !tableNames.Contains(name)).ToList();

        Assert.IsEmpty(stale, $"Classified options the option table no longer has: --{string.Join(", --", stale)}");
    }

    [TestMethod]
    public void TryFind_UnknownOption_ReturnsFalseSoTheServerRefusesIt()
    {
        var found = McpOptionPathTable.TryFind("no-such-option", out var use);

        Assert.IsFalse(found);
        Assert.AreEqual(McpOptionPathUse.None, use);
    }

    [TestMethod]
    public void TryFind_LongNameInAnotherCase_ReturnsFalse()
    {
        var found = McpOptionPathTable.TryFind("Output", out _);

        Assert.IsFalse(found);
    }

    [TestMethod]
    public void TryFind_NullLongName_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => McpOptionPathTable.TryFind(null!, out _));
    }

    [TestMethod]
    // -o: "Write output to the given file instead of stdout."
    [DataRow("output")]
    // -O: "Write output to a local file named like the remote file we get."
    [DataRow("remote-name")]
    // --remote-name-all: "This option changes the default action for all given URLs to be dealt with as if -O, --remote-name were used for each one."
    [DataRow("remote-name-all")]
    // -J: "This option tells the -O, --remote-name option to use the server-specified Content-Disposition filename instead of extracting a filename from the URL."
    [DataRow("remote-header-name")]
    // -D: "Write the received protocol headers to the specified file."
    [DataRow("dump-header")]
    // -c: "Specify to which file you want curl to write all cookies after a completed operation."
    [DataRow("cookie-jar")]
    // --trace: "Save a full trace dump of all incoming and outgoing data, including descriptive information, in the given output file."
    [DataRow("trace")]
    // --trace-ascii: "Save a full trace dump of all incoming and outgoing data, including descriptive information, in the given output file."
    [DataRow("trace-ascii")]
    // --stderr: "Redirect all writes to stderr to the specified file instead."
    [DataRow("stderr")]
    // --etag-save: "Save an HTTP ETag to the specified file."
    [DataRow("etag-save")]
    // --libcurl: "Append this option to any ordinary curl command line, and you get libcurl-using C source code written to the file that does the equivalent of what your command-line operation does!"
    [DataRow("libcurl")]
    // --log-file (Curl's diagnostic log): writes its diagnostic log to the named file.
    [DataRow("log-file")]
    public void TryFind_OptionWritingAFile_IsWritesFile(string longName)
    {
        AssertUse(longName, McpOptionPathUse.WritesFile);
    }

    [TestMethod]
    // --output-dir: "Specify the directory in which files should be stored, when -O, --remote-name or -o, --output are used."
    [DataRow("output-dir")]
    public void TryFind_OptionNamingAnOutputDirectory_IsWritesDirectory(string longName)
    {
        AssertUse(longName, McpOptionPathUse.WritesDirectory);
    }

    [TestMethod]
    // -T: "Upload the specified local file to the remote URL."
    [DataRow("upload-file")]
    // -K: "Specify a text file to read curl arguments from."
    [DataRow("config")]
    // --cacert: "Use the specified certificate file to verify the peer."
    [DataRow("cacert")]
    // --capath: "Use the specified certificate directory to verify the peer."
    [DataRow("capath")]
    // --cert: "Use the specified client certificate file when getting a file with HTTPS, FTPS or another SSL-based protocol."
    [DataRow("cert")]
    // --key: "Private key filename."
    [DataRow("key")]
    // --crlfile: "Provide a file using PEM format with a Certificate Revocation List."
    [DataRow("crlfile")]
    // --netrc-file: "Set the netrc file to use."
    [DataRow("netrc-file")]
    // -n: "Makes curl scan the .netrc file in the user's home directory for login name and password."
    [DataRow("netrc")]
    // --netrc-optional: "Similar to -n, --netrc, but this option makes the .netrc usage optional."
    [DataRow("netrc-optional")]
    // --etag-compare: "Make a conditional HTTP request for the specific ETag read from the given file."
    [DataRow("etag-compare")]
    // --pubkey: "Public key filename."
    [DataRow("pubkey")]
    // --knownhosts: "When doing SCP and SFTP transfers, curl automatically checks a database containing identification for all hosts it has ever been used with" - this option names that file.
    [DataRow("knownhosts")]
    // --proxy-cacert: "Same as --cacert but used in HTTPS proxy context."
    [DataRow("proxy-cacert")]
    // --proxy-capath: "Same as --capath but used in HTTPS proxy context."
    [DataRow("proxy-capath")]
    // --proxy-cert: "Same as -E, --cert but used in HTTPS proxy context."
    [DataRow("proxy-cert")]
    // --proxy-key: "Same as --key but used in HTTPS proxy context."
    [DataRow("proxy-key")]
    // --proxy-crlfile: "Same as --crlfile but used in HTTPS proxy context."
    [DataRow("proxy-crlfile")]
    // --httpsig-key (Curl's HTTP message signatures): the private key file to sign with.
    [DataRow("httpsig-key")]
    public void TryFind_OptionReadingAFile_IsReadsFile(string longName)
    {
        AssertUse(longName, McpOptionPathUse.ReadsFile);
    }

    [TestMethod]
    // --hsts: "Enable HSTS for the transfer. If the filename points to an existing HSTS cache file, that is used. After a completed transfer, the cache is saved to the filename again if it has been modified."
    [DataRow("hsts")]
    // --alt-svc: "Enable the alt-svc parser. If the filename points to an existing alt-svc cache file, that gets used. After a completed transfer, the cache is saved to the filename again if it has been modified."
    [DataRow("alt-svc")]
    // --ssl-sessions: "Use the given file to load SSL session tickets into curl's cache before starting any transfers. At the end of a successful curl run, the cached SSL sessions tickets are saved to the file."
    [DataRow("ssl-sessions")]
    public void TryFind_OptionReadingAndSavingACache_IsReadsOrWritesFile(string longName)
    {
        AssertUse(longName, McpOptionPathUse.ReadsOrWritesFile);
    }

    [TestMethod]
    // -b: "If no '=' symbol is used in the argument, it is instead treated as a filename to read previously stored cookie from."
    [DataRow("cookie")]
    // --pinnedpubkey: "Use the specified public key file (or hashes) to verify the peer."
    [DataRow("pinnedpubkey")]
    // --proxy-pinnedpubkey: "Use the specified public key file (or hashes) to verify the proxy."
    [DataRow("proxy-pinnedpubkey")]
    // -z: "If the given expression is a filename, curl uses the modification time of that file."
    [DataRow("time-cond")]
    public void TryFind_OptionTakingAFileOrALiteral_IsReadsFileOrLiteral(string longName)
    {
        AssertUse(longName, McpOptionPathUse.ReadsFileOrLiteral);
    }

    [TestMethod]
    // -d: "If you start the data with the letter @, the rest should be a filename to read the data from."
    [DataRow("data")]
    // --data-ascii: "This is just an alias for -d, --data."
    [DataRow("data-ascii")]
    // --data-binary: "If you start the data with the letter @, the rest should be a filename."
    [DataRow("data-binary")]
    // --data-urlencode: "name@filename - This makes curl load data from the given file."
    [DataRow("data-urlencode")]
    // --json: "If you start the data with the letter @, the rest should be a filename to read the data from."
    [DataRow("json")]
    // --url-query: "name@filename - load data from the given file."
    [DataRow("url-query")]
    // -F: "To force the 'content' part to be a file, prefix the filename with an @ sign. To just get the content part from a file, prefix the filename with the symbol <."
    [DataRow("form")]
    // -H: "If you start the data with the letter @, the rest should be a filename to read the headers from."
    [DataRow("header")]
    // --proxy-header: "This option can take an argument in @filename style."
    [DataRow("proxy-header")]
    // -w: "If the format string starts with an '@', curl reads the format from the given filename"; "%output{filename}" switches output to a file.
    [DataRow("write-out")]
    // --variable: "name@filename - set the variable to the contents of the given file."
    [DataRow("variable")]
    // --url: the URL may be "file://" and name a local file.
    [DataRow("url")]
    // -x: "To connect to a SOCKS proxy over a Unix domain socket, use socks5h://localhost/path/to/socket.sock."
    [DataRow("proxy")]
    // --preproxy: "Use the specified SOCKS proxy before connecting to an HTTP or HTTPS -x, --proxy."
    [DataRow("preproxy")]
    // --socks5-hostname: "Use the specified SOCKS5 proxy (and let the proxy resolve the hostname)." The proxy may be a Unix socket URL.
    [DataRow("socks5-hostname")]
    [DataRow("socks5")]
    [DataRow("socks4")]
    [DataRow("socks4a")]
    public void TryFind_OptionWhoseValueMayCarryAPath_IsValueEmbedded(string longName)
    {
        AssertUse(longName, McpOptionPathUse.ValueEmbedded);
    }

    [TestMethod]
    // --unix-socket: "Connect through this Unix domain socket, instead of using the network."
    [DataRow("unix-socket")]
    // --abstract-unix-socket: "Connect through an abstract Unix domain socket, instead of using the network."
    [DataRow("abstract-unix-socket")]
    public void TryFind_OptionNamingAUnixSocket_IsUnixSocketPath(string longName)
    {
        AssertUse(longName, McpOptionPathUse.UnixSocketPath);
    }

    [TestMethod]
    // --data-raw: "This posts data similarly to -d, --data but without the special interpretation of the @ character."
    [DataRow("data-raw")]
    // --form-string: "Similar to -F, --form except that the value string for the named parameter is used literally. Leading @ and < characters [...] have no special meaning."
    [DataRow("form-string")]
    // --random-file: "Deprecated option. This option is ignored by curl since 7.84.0."
    [DataRow("random-file")]
    // --egd-file: "Deprecated option. This option is ignored by curl since 7.84.0."
    [DataRow("egd-file")]
    // -s: "Silent or quiet mode." No path.
    [DataRow("silent")]
    // --create-dirs: "When used in conjunction with the -o, --output option, curl creates the necessary local directory hierarchy as needed." It names no path of its own.
    [DataRow("create-dirs")]
    public void TryFind_OptionNamingNoPath_IsNone(string longName)
    {
        AssertUse(longName, McpOptionPathUse.None);
    }

    private static void AssertUse(string longName, McpOptionPathUse expected)
    {
        var found = McpOptionPathTable.TryFind(longName, out var use);

        Assert.IsTrue(found, $"--{longName} is not classified");
        Assert.AreEqual(expected, use, $"--{longName}");
    }
}
