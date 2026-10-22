using System.Text;
using Curl.Cli;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance.UnitProgram.UrlParsing;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit1627.c</c>: <c>Curl_get_scheme</c>, curl's one table of the
/// schemes it knows, transfer and proxy schemes alike. Curl keeps that table in two places, so the lookup asks
/// <see cref="CommandLineProtocolSet.KnownScheme"/> (the transfer schemes) and then
/// <see cref="CommandLineProxy.TryGetKind"/> (the proxy schemes, <c>socks</c> to <c>socks5h</c>). Every
/// <c>okay</c> name must be found, under its own name compared case-insensitively and with that name in lower
/// case; every <c>notokay</c> name must not be. As in the C program, each loop stops at its first wrong row and
/// writes its line to standard output, then <c>&lt;i + j&gt; invokes</c> is written, and the program returns
/// <c>CURLE_FAILED_INIT</c> (2) when either loop stopped early.
/// </summary>
/// <param name="getScheme">Finds a scheme by name, returning its lower-case name or <see langword="null"/>; <see cref="FindKnownScheme"/> unless a test injects another.</param>
public sealed class Unit1627SchemeLookup(Func<string, string?> getScheme) : IUnitProgram
{
    private const int FailedInit = 2;

    private static readonly string[] Okay =
    [
        "DICT", "FILE", "FTP", "FTPS", "GOPHER", "GOPHERS", "HTTP", "HTTPS",
        "IMAP", "IMAPS", "LDAP", "LDAPS", "MQTT", "MQTTS", "POP3", "POP3S",
        "RTSP", "SCP", "SFTP", "SMB", "SMBS", "SMTP", "SMTPS",
        "TELNET", "TFTP", "WS", "WSS",
        "dict", "file", "ftp", "ftps", "gopher", "gophers", "http", "https",
        "imap", "imaps", "ldap", "ldaps", "mqtt", "mqtts", "pop3", "pop3s",
        "rtsp", "scp", "sftp", "smb", "smbs", "smtp", "smtps",
        "telnet", "tftp", "ws", "wss",
        "diCt", "fIle", "Ftp", "ftpS", "Gopher", "gOphers", "htTp", "httPs",
        "imAP", "imaPS", "LDap", "LDAps", "mQTT", "mqtTS", "pOP3", "pOP3s",
        "RtsP", "ScP", "SFtP", "Smb", "smBS", "sMTP", "SMTPs",
        "TELNEt", "tFTP", "Ws", "wSS",
        "SOCKS", "SOCKS4", "SOCKS5", "SOCKS4A", "SOCKS5H",
        "socks", "socks4", "socks5", "socks4a", "socks5h",
        "Socks", "sOcks4", "soCks5", "socKs4a", "sockS5h",
    ];

    private static readonly string[] NotOkay =
    [
        "a", "A", "ht", "tt", "tt", "p+", "TPP", "PPS", "TSP",
        "HER", "1CT", "AbG", "LQp", "rtW", "PkY", "xZq", "LmO",
        "hyT", "wQA", "dfG", "BvC", "iuY", "ewQ", "dfG", "Jkl",
        "NbV", "cXz", "OiU", "SrE", "QaS", "ghJ", "LmN", "VcX", "PoI",
        "YtR", "WqA", "DfG", "JkL", "XcV", "NmM", "WeR", "YuI", "PaS",

        "a", "A", "htt", "ttp", "httt", "http+", "HTTPP", "HTTPPS", "HTTSP",
        "GROPHER", "D1CT", "AbG", "zLQp", "mNrtW", "PkY", "bVcxZq", "LmO",
        "iUhyT", "rEwQA", "xSdfG", "nBvC", "pOiuY", "tRewQ", "aSdfG", "hJkl",
        "mNbV", "cXz", "pOiU", "yTrE", "wQaS", "dFghJ", "kLmN", "bVcX", "zPoI",
        "uYtR", "eWqA", "sDfG", "hJkL", "zXcV", "bNmM", "qWeR", "tYuI", "oPaS",
        "dFgH", "jKlZ", "xCvB", "nMqW", "eRtY", "uIoP", "aSdF", "gHjK", "lZxC",
        "vBnM", "QwEr", "TyUi", "OpAs", "DfGh", "JkLz", "XcVb", "NmqW", "ErTy",
        "UiOp", "AsDf", "GhJk", "LzXc", "VbNm", "qweR", "tyuI", "opaD", "fghJ",
        "klzx", "cvbn", "mQW", "ErTy", "UiOp", "AsDf", "GhJk", "LzXc", "VbNm",
        "QwEr", "TyUi", "OpAs", "DfGh", "JkLz", "XcVb", "NmqW", "ErTy", "UiOp",
        "AsDf", "GhJk", "LzXc", "VbNm", "qWeR", "tYuI", "oPaS", "dFgH", "jKlZ",
        "xCvB", "nMqW", "eRtY", "uIoP", "aSdF", "gHjK", "lZxC", "vBnM", "QwEr",
        "TyUi", "OpAs", "DfGh", "JkLz", "XcVb", "NmqW", "ErTy", "UiOp", "AsDf",
        "GhJk", "LzXc", "VbNm",

        "aa", "Aa", "htta", "ttpa", "httta", "http+a", "HTTPPa", "HTTPPSa",
        "HTTSPa", "GROPHERa", "D1CTa", "AbGa", "zLQpa", "mNrtWa", "PkYa",
        "bVcxZqa", "LmOa", "iUhyTa", "rEwQAa", "xSdfGa", "nBvCa", "pOiuYa",
        "tRewQa", "aSdfGa", "hJkla", "mNbVa", "cXza", "pOiUa", "yTrEa", "wQaSa",
        "dFghJa", "kLmNa", "bVcXa", "zPoIa", "uYtRa", "eWqAa", "sDfGa", "hJkLa",
        "zXcVa", "bNmMa", "qWeRa", "tYuIa", "oPaSa", "dFgHa", "jKlZa", "xCvBa",
        "nMqWa", "eRtYa", "uIoPa", "aSdFa", "gHjKa", "lZxCa", "vBnMa", "QwEra",
        "TyUia", "OpAsa", "DfGha", "JkLza", "XcVba", "NmqWa", "ErTya", "UiOpa",
        "AsDfa", "GhJka", "LzXca", "VbNma", "qweRa", "tyuIa", "opaDa", "fghJa",
        "klzxa", "cvbna", "mQWa", "ErTya", "UiOpa", "AsDfa", "GhJka", "LzXca",
        "VbNma", "QwEra", "TyUia", "OpAsa", "DfGha", "JkLza", "XcVba", "NmqWa",
        "ErTya", "UiOpa", "AsDfa", "GhJka", "LzXca", "VbNma", "qWeRa", "tYuIa",
        "oPaSa", "dFgHa", "jKlZa", "xCvBa", "nMqWa", "eRtYa", "uIoPa", "aSdFa",
        "gHjKa", "lZxCa", "vBnMa", "QwEra", "TyUia", "OpAsa", "DfGha", "JkLza",
        "XcVba", "NmqWa", "ErTya", "UiOpa", "AsDfa", "GhJka", "LzXca", "VbNma",

        "aab", "Aab", "httab", "ttpab", "htttab", "http+ab", "HTTPPab", "HTTPPSab",
        "HTTSPab", "GROPHERab", "D1CTab", "AbGab", "zLQpab", "mNrtWab", "PkYab",
        "bVcxZqab", "LmOab", "iUhyTab", "rEwQAab", "xSdfGab", "nBvCab", "pOiuYab",
        "tRewQab", "aSdfGab", "hJklab", "mNbVab", "cXzab", "pOiUab", "yTrEab",
        "wQaSab", "dFghJab", "kLmNab", "bVcXab", "zPoIab", "uYtRab", "eWqAab",
        "sDfGab", "hJkLab", "zXcVab", "bNmMab", "qWeRab", "tYuIab", "oPaSab",
        "dFgHab", "jKlZab", "xCvBab", "nMqWab", "eRtYab", "uIoPab", "aSdFab",
        "gHjKab", "lZxCab", "vBnMab", "QwErab", "TyUiab", "OpAsab", "DfGhab",
        "JkLzab", "XcVbab", "NmqWab", "ErTyab", "UiOpab", "AsDfab", "GhJkab",
        "LzXcab", "VbNmab", "qweRab", "tyuIab", "opaDab", "fghJab", "klzxab",
        "cvbnab", "mQWab", "ErTyab", "UiOpab", "AsDfab", "GhJkab", "LzXcab",
        "VbNmab", "QwErab", "TyUiab", "OpAsab", "DfGhab", "JkLzab", "XcVbab",
        "NmqWab", "ErTyab", "UiOpab", "AsDfab", "GhJkab", "LzXcab", "VbNmab",
        "qWeRab", "tYuIab", "oPaSab", "dFgHab", "jKlZab", "xCvBab", "nMqWab",
        "eRtYab", "uIoPab", "aSdFab", "gHjKab", "lZxCab", "vBnMab", "QwErab",
        "TyUiab", "OpAsab", "DfGhab", "JkLzab", "XcVbab", "NmqWab", "ErTyab",
        "UiOpab", "AsDfab", "GhJkab", "LzXcab", "VbNmab",

        "dictt", "filee", "ftpp", "ftpss", "gopherr", "gopherss", "httpp",
        "httpss", "imapp", "imapss", "ldapp", "ldapss", "mqttt", "mqttss",
        "pop33", "pop3ss", "rtspp", "scpp", "sftpp", "smbb", "smbss", "smtpp",
        "smtpss", "telnett", "tftpp", "wsw", "wsss",

        "DIC", "FIL", "FT", "TPS", "GOPHE", "OPHER", "HTT", "TTPS",
        "IMA", "APS", "LDA", "APS", "MQT", "TTS", "POP", "P3S",
        "RTS", "SC", "SFT", "SM", "BS", "SMT", "TPS",
        "TELNE", "TFT", "W", "S",
    ];

    /// <summary>Creates the port over <see cref="FindKnownScheme"/>.</summary>
    public Unit1627SchemeLookup()
        : this(FindKnownScheme)
    {
    }

    /// <inheritdoc />
    public string ProgramName => "unit1627";

    /// <summary>
    /// Curl's counterpart of <c>Curl_get_scheme</c>: the lower-case name of a transfer scheme
    /// <see cref="CommandLineProtocolSet.KnownScheme"/> knows, else of a proxy scheme
    /// <see cref="CommandLineProxy.TryGetKind"/> accepts, else <see langword="null"/>.
    /// </summary>
    /// <param name="name">The scheme, in any case.</param>
    /// <returns>The scheme's lower-case name, or <see langword="null"/> when curl knows no such scheme.</returns>
    public static string? FindKnownScheme(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        bool isProxyScheme = new CommandLineProxy(name + "://host", ProxyKind.Http).TryGetKind(out _, out _);

        return CommandLineProtocolSet.KnownScheme(name) ?? (isProxyScheme ? name.ToLowerInvariant() : null);
    }

    /// <inheritdoc />
    public async Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        StringBuilder output = new();
        int i = CountOkayUntilWrong(output);
        int j = CountNotOkayUntilWrong(output);
        output.Append($"{i + j} invokes\n");
        await context.StandardOutput.WriteAsync(Encoding.ASCII.GetBytes(output.ToString()), cancellationToken).ConfigureAwait(false);

        return i != Okay.Length || j != NotOkay.Length ? new UnitProgramResult(FailedInit) : UnitProgramResult.Success;
    }

    private int CountOkayUntilWrong(StringBuilder output)
    {
        int i = 0;
        for (; i < Okay.Length; i++)
        {
            string name = Okay[i];
            string? found = getScheme(name);
            if (found is null || !string.Equals(found, name, StringComparison.OrdinalIgnoreCase))
            {
                output.Append($"Input: {name}, expected okay\n");
                break;
            }

            string lowered = name.ToLowerInvariant();
            if (!string.Equals(lowered, found, StringComparison.Ordinal))
            {
                output.Append($"Input: {lowered} is not lowercase: {found}\n");
                break;
            }
        }

        return i;
    }

    private int CountNotOkayUntilWrong(StringBuilder output)
    {
        int j = 0;
        for (; j < NotOkay.Length; j++)
        {
            if (getScheme(NotOkay[j]) is not null)
            {
                output.Append($"Input: {NotOkay[j]}, expected not okay\n");
                break;
            }
        }

        return j;
    }
}
