using System.Globalization;
using System.Text.RegularExpressions;
using System.Text;

namespace Curl.Conformance;

/// <summary>
/// The data-transfer commands of upstream's <c>tests/ftpserver.pl</c> (at <c>curl-8_21_0</c>)
/// for one control connection: <c>PASV</c> and <c>EPSV</c> open an <see cref="FtpDataConnection"/>
/// on <see cref="FtpServerConnector.PassivePort"/>, and <c>PORT</c> and <c>EPRT</c> connect one to
/// the port curl listens on; <c>RETR</c>, <c>LIST</c> and <c>NLST</c> send into it and close it,
/// and <c>STOR</c> and <c>APPE</c> hand it over as the upload, whose bytes are what ftpserver.pl
/// writes to its upload file; under <c>/// writes to its upload file; <c>SIZE</c>, <c>MDTM</c> and <c>REST</c>lt;servercmd/// writes to its upload file; <c>SIZE</c>, <c>MDTM</c> and <c>REST</c>gt;</c> <c>NODATACONN</c> (or its 425, 421 and 150 variants) no data connection is made and the transfer commands answer as ftpserver.pl does without one; <c>SIZE</c>, <c>MDTM</c> and <c>REST</c>
/// answer from the case's <c>&lt;reply&gt;</c> parts. Each answer is what ftpserver.pl's handler
/// sends after the command's display text. A file name loads the case's data when, as
/// ftpserver.pl reads it, it names a test number: ftpserver.pl then loads that test's file, which
/// in a case's log directory is the case's own.
/// </summary>
internal sealed class FtpTransferCommands
{
    private const string AsciiTransferComplete = "226 ASCII transfer complete\r\n";

    private const string FileTransferComplete = "226 File transfer complete\r\n";

    private const string GimmeGimme = "125 Gimme gimme gimme!\r\n";

    private const string SillyYou = "500 silly you, go away\r\n";

    private static readonly Regex PortArgument = new(@"(\d+),(\d+),(\d+),(\d+),(\d+),(\d+)", RegexOptions.CultureInvariant);

    private static readonly Regex EprtArgument = new(@"(\d+)\|([^\|]+)\|(\d+)", RegexOptions.CultureInvariant);

    private static readonly byte[] NameList = Encoding.Latin1.GetBytes("file\r\nwith space\r\nfake\r\n..\r\n ..\r\nfunny\r\nREADME\r\n");

    private readonly UpstreamTestCase testCase;

    private readonly Action<FtpDataConnection> openPassive;

    private readonly Func<int, FtpDataConnection?> connectActive;

    private readonly Action<FtpDataConnection> receiveUpload;

    private readonly string[] serverCommandLines;

    private readonly Dictionary<string, Func<string, string>> handlers;

    private FtpDataConnection? dataConnection;

    private long restartOffset;

    private bool weirdRetrieve;

    /// <summary>Creates the commands for one control connection.</summary>
    /// <param name="testCase">The expanded case whose <c>&lt;reply&gt;</c> parts are served.</param>
    /// <param name="openPassive">Called with each data connection <c>PASV</c> or <c>EPSV</c> opens, for the client to connect to.</param>
    /// <param name="connectActive">Connects to the port <c>PORT</c> or <c>EPRT</c> names, returning the server's end, or <see langword="null"/> when nothing listens there.</param>
    /// <param name="receiveUpload">Called with the data connection each <c>STOR</c> or <c>APPE</c> uploads into.</param>
    public FtpTransferCommands(UpstreamTestCase testCase, Action<FtpDataConnection> openPassive, Func<int, FtpDataConnection?> connectActive, Action<FtpDataConnection> receiveUpload)
    {
        this.testCase = testCase;
        this.openPassive = openPassive;
        this.connectActive = connectActive;
        this.receiveUpload = receiveUpload;
        serverCommandLines = UpstreamTestPartBodies.Lines(testCase.Find("reply", "servercmd"));
        weirdRetrieve = HasServerCommand("RETRWEIRDO");
        handlers = new(StringComparer.Ordinal)
        {
            ["PASV"] = _ => OpenPassive(extended: false),
            ["EPSV"] = _ => OpenPassive(extended: true),
            ["PORT"] = Port,
            ["EPRT"] = ExtendedPort,
            ["STOR"] = Store,
            ["APPE"] = Store,
            ["LIST"] = _ => Transfer(Listing(), AsciiTransferComplete),
            ["NLST"] = _ => Transfer(NameList, AsciiTransferComplete),
            ["RETR"] = Retrieve,
            ["SIZE"] = Size,
            ["MDTM"] = ModificationTime,
            ["REST"] = Restart,
        };
    }

    /// <summary>Answers a transfer command.</summary>
    /// <param name="command">The command in upper case.</param>
    /// <param name="argument">Its argument.</param>
    /// <param name="answer">The text sent after the command's display text; empty to send nothing more.</param>
    /// <returns><see langword="true"/> when the command is one of these.</returns>
    public bool TryAnswer(string command, string argument, out string answer)
    {
        if (!handlers.TryGetValue(command, out Func<string, string>? handler))
        {
            answer = string.Empty;
            return false;
        }

        answer = handler(argument);
        return true;
    }

    private bool HasServerCommand(string name) =>
        serverCommandLines.Any(line => line.Contains(name, StringComparison.Ordinal));

    // NODATACONN, or any of NODATACONN425, NODATACONN421 and NODATACONN150, which imply it: the
    // passive port is bound but not listening, so curl's connect is refused, and PORT or EPRT
    // connects to nothing.
    private bool RefusesDataConnections => HasServerCommand("NODATACONN");

    // What LIST, NLST, RETR, STOR and APPE answer when no data connection is up, as ftpserver.pl's
    // handlers do, checking NODATACONN425 first, then 421, then 150; otherwise the client times out.
    private string NoDataConnectionAnswer()
    {
        const string Opening = "150 Opening data connection\r\n";
        if (HasServerCommand("NODATACONN425"))
        {
            return Opening + "425 Can't open data connection\r\n";
        }

        if (HasServerCommand("NODATACONN421"))
        {
            return Opening + "421 Connection timed out\r\n";
        }

        return HasServerCommand("NODATACONN150") ? Opening : string.Empty;
    }

    private string OpenPassive(bool extended)
    {
        dataConnection = RefusesDataConnections ? null : new FtpDataConnection();
        if (dataConnection is not null)
        {
            openPassive(dataConnection);
        }

        const int port = FtpServerConnector.PassivePort;
        string address = HasServerCommand("PASVBADIP") ? "1,2,3,4" : "127,0,0,1";
        return extended
            ? $"229 Entering Passive Mode (|||{port}|)\r\n"
            : $"227 Entering Passive Mode ({address},{port / 256},{port % 256})\r\n";
    }

    // ftpserver.pl's PORT_ftp: a PORT line it cannot read gets 500 after the display text, and a
    // readable one nothing more; it then connects to the port, ignoring the address.
    private string Port(string argument)
    {
        Match match = PortArgument.Match(argument);
        return match.Success
            ? ConnectActive((LeadingNumber(match.Groups[5].Value) << 8) + LeadingNumber(match.Groups[6].Value), string.Empty)
            : SillyYou;
    }

    private string ExtendedPort(string argument)
    {
        Match match = EprtArgument.Match(argument);
        return match.Success
            ? ConnectActive(LeadingNumber(match.Groups[3].Value), "200 Thanks for dropping by. We contact you later\r\n")
            : SillyYou;
    }

    // Port 0 or one past 65535 starts no data connection, and the client waits for one.
    private string ConnectActive(long port, string answer)
    {
        dataConnection = port is > 0 and <= 65535 && !RefusesDataConnections ? connectActive((int)port) : null;
        return answer;
    }

    // ftpserver.pl's STOR_ftp reads the upload until the client closes the data connection, then
    // sends the <servercmd> STOR line's text or 226; here the client's bytes are kept as it writes them.
    private string Store(string argument)
    {
        if (dataConnection is null)
        {
            return NoDataConnectionAnswer();
        }

        receiveUpload(dataConnection);
        dataConnection = null;
        string? storeResponse = serverCommandLines.FirstOrDefault(line => line.StartsWith("STOR ", StringComparison.Ordinal));
        return GimmeGimme + (storeResponse is null ? FileTransferComplete : storeResponse[5..] + "\r\n");
    }

    // Without a data connection ftpserver.pl sends nothing after the display text, unless a NODATACONN variant says otherwise.
    private string Transfer(byte[] data, string completion)
    {
        if (dataConnection is null)
        {
            return NoDataConnectionAnswer();
        }

        dataConnection.Send(data);
        dataConnection.Close();
        dataConnection = null;
        return completion;
    }

    private byte[] Listing()
    {
        string text = Encoding.Latin1.GetString(PartBytes("data"));
        return Encoding.Latin1.GetBytes(text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal));
    }

    private string Retrieve(string argument)
    {
        if (dataConnection is null)
        {
            return NoDataConnectionAnswer();
        }

        int firstDigit = argument.AsSpan().IndexOfAnyInRange('0', '9');
        (string testNumber, string partNumber, bool loads) = TestFile(firstDigit < 0 ? string.Empty : argument[firstDigit..]);
        byte[]? data = loads ? RetrievableData(partNumber) : null;
        if (data is null)
        {
            return $"550 {testNumber}: No such file or directory.\r\n";
        }

        long size = data.Length - restartOffset;
        restartOffset = 0;
        bool weird = weirdRetrieve;
        weirdRetrieve = false;
        return weird
            ? $"150 Binary data connection for {testNumber} () ({size} bytes).\r\n{FileTransferComplete}" + Transfer(data, string.Empty)
            : $"150 Binary data connection for {testNumber} ({partNumber}) {SizeText(size)}.\r\n" + Transfer(data, FileTransferComplete);
    }

    // ftpserver.pl answers 550 for a missing part, and for an empty one unless it says sendzero.
    private byte[]? RetrievableData(string partNumber)
    {
        UpstreamTestSection? part = testCase.Find("reply", "data" + partNumber);
        if (part is null)
        {
            return null;
        }

        byte[] data = UpstreamTestPartBodies.Decoded(part);
        return data.Length > 0 || part.IsAttributeSet("sendzero") ? data : null;
    }

    private string SizeText(long size)
    {
        if (HasServerCommand("RETRNOSIZE"))
        {
            return "size?";
        }

        long announced = serverCommandLines
            .Select(line => line.Split("RETRSIZE ", 2))
            .Where(split => split.Length == 2)
            .Select(split => LeadingNumber(split[1]))
            .FirstOrDefault();
        return $"({(announced > 0 ? announced : size)} bytes)";
    }

    // ftpserver.pl matches the argument with /(\d+)\/?$/ and sends nothing when it does not.
    private string Size(string argument)
    {
        string trimmed = argument.EndsWith('/') ? argument[..^1] : argument;
        string digits = trimmed[(trimmed.AsSpan().LastIndexOfAnyExceptInRange('0', '9') + 1)..];
        if (digits.Length == 0)
        {
            return string.Empty;
        }

        (string testNumber, string partNumber, _) = TestFile(digits);
        bool exists = TryFindSize(partNumber, out long size);
        return exists ? $"213 {size}\r\n" : $"550 {testNumber}: No such file or directory.\r\n";
    }

    // A <size> part's first number wins, and exists from 0 up; else the data part's length, from 1 up.
    private bool TryFindSize(string partNumber, out long size)
    {
        string[] sizeLines = UpstreamTestPartBodies.Lines(testCase.Find("reply", "size"));
        if (sizeLines.Length > 0)
        {
            size = LeadingNumber(sizeLines[0]);
            return size > -1;
        }

        size = PartBytes("data" + partNumber).Length;
        return size > 0;
    }

    private string ModificationTime(string argument)
    {
        (string testNumber, _, bool loads) = TestFile(argument);
        string reply = loads ? FirstModificationTimeLine() : string.Empty;
        return IsNegativeNumber(reply) ? $"550 {testNumber}: no such file.\r\n" : ModificationTimeAnswer(reply);
    }

    private string FirstModificationTimeLine() =>
        UpstreamTestPartBodies.Text(testCase.Find("reply", "mdtm")).Split('\n')[0];

    private static string ModificationTimeAnswer(string reply) =>
        reply is "" or "0" ? "500 MDTM: no such command.\r\n" : reply + "\r\n";

    // ftpserver.pl tests the reply with /^-?\d+$/ and then $mdtm < 0.
    private static bool IsNegativeNumber(string reply) =>
        reply.StartsWith('-') && IsAllDigits(reply[1..]) && LeadingNumber(reply) < 0;

    private static bool IsAllDigits(string text) => !text.AsSpan().ContainsAnyExceptInRange('0', '9');

    private string Restart(string argument)
    {
        restartOffset = LeadingNumber(argument);
        return string.Empty;
    }

    private byte[] PartBytes(string name) =>
        testCase.Find("reply", name) is { } part ? UpstreamTestPartBodies.Decoded(part) : [];

    /// <summary>
    /// Reads a file name as ftpserver.pl does: a number over 10000 is the test number times 10000
    /// plus the part number; the test's file loads when what is left is a test number.
    /// </summary>
    private static (string TestNumber, string PartNumber, bool Loads) TestFile(string name)
    {
        long number = LeadingNumber(name);
        bool numbered = number > 10000;
        string testNumber = numbered ? (number / 10000).ToString(CultureInfo.InvariantCulture) : name;
        string partNumber = numbered ? (number % 10000).ToString(CultureInfo.InvariantCulture) : string.Empty;
        return (testNumber, partNumber, testNumber.Length > 0 && IsAllDigits(testNumber));
    }

    /// <summary>The number Perl reads from the start of a string: an optional minus sign and up to 18 digits, else 0.</summary>
    private static long LeadingNumber(string text)
    {
        string trimmed = text.TrimStart();
        bool negative = trimmed.StartsWith('-');
        ReadOnlySpan<char> rest = trimmed.AsSpan(negative ? 1 : 0);
        int end = rest.IndexOfAnyExceptInRange('0', '9');
        ReadOnlySpan<char> digits = rest[..Math.Min(18, end < 0 ? rest.Length : end)];
        long magnitude = digits.IsEmpty ? 0 : long.Parse(digits, CultureInfo.InvariantCulture);
        return negative ? -magnitude : magnitude;
    }
}
