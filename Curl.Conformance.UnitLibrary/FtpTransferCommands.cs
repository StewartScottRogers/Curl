using System.Globalization;
using System.Text;

namespace Curl.Conformance;

/// <summary>
/// The passive-mode data-transfer commands of upstream's <c>tests/ftpserver.pl</c> (at
/// <c>curl-8_21_0</c>) for one control connection: <c>PASV</c> and <c>EPSV</c> open an
/// <see cref="FtpDataConnection"/> on <see cref="FtpServerConnector.PassivePort"/>; <c>RETR</c>,
/// <c>LIST</c> and <c>NLST</c> send into it and close it; <c>SIZE</c>, <c>MDTM</c> and <c>REST</c>
/// answer from the case's <c>&lt;reply&gt;</c> parts. Each answer is what ftpserver.pl's handler
/// sends after the command's display text. A file name loads the case's data when, as
/// ftpserver.pl reads it, it names a test number: ftpserver.pl then loads that test's file, which
/// in a case's log directory is the case's own.
/// </summary>
internal sealed class FtpTransferCommands
{
    private const string AsciiTransferComplete = "226 ASCII transfer complete\r\n";

    private const string FileTransferComplete = "226 File transfer complete\r\n";

    private static readonly byte[] NameList = Encoding.Latin1.GetBytes("file\r\nwith space\r\nfake\r\n..\r\n ..\r\nfunny\r\nREADME\r\n");

    private readonly UpstreamTestCase testCase;

    private readonly Action<FtpDataConnection> openPassive;

    private readonly string[] serverCommandLines;

    private readonly Dictionary<string, Func<string, string>> handlers;

    private FtpDataConnection? dataConnection;

    private long restartOffset;

    private bool weirdRetrieve;

    /// <summary>Creates the commands for one control connection.</summary>
    /// <param name="testCase">The expanded case whose <c>&lt;reply&gt;</c> parts are served.</param>
    /// <param name="openPassive">Called with each data connection <c>PASV</c> or <c>EPSV</c> opens, for the client to connect to.</param>
    public FtpTransferCommands(UpstreamTestCase testCase, Action<FtpDataConnection> openPassive)
    {
        this.testCase = testCase;
        this.openPassive = openPassive;
        serverCommandLines = UpstreamTestPartBodies.Lines(testCase.Find("reply", "servercmd"));
        weirdRetrieve = HasServerCommand("RETRWEIRDO");
        handlers = new(StringComparer.Ordinal)
        {
            ["PASV"] = _ => OpenPassive(extended: false),
            ["EPSV"] = _ => OpenPassive(extended: true),
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

    private string OpenPassive(bool extended)
    {
        dataConnection = new FtpDataConnection();
        openPassive(dataConnection);
        const int port = FtpServerConnector.PassivePort;
        string address = HasServerCommand("PASVBADIP") ? "1,2,3,4" : "127,0,0,1";
        return extended
            ? $"229 Entering Passive Mode (|||{port}|)\r\n"
            : $"227 Entering Passive Mode ({address},{port / 256},{port % 256})\r\n";
    }

    // Without a data connection ftpserver.pl sends nothing after the display text.
    private string Transfer(byte[] data, string completion)
    {
        if (dataConnection is null)
        {
            return string.Empty;
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
            return string.Empty;
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
