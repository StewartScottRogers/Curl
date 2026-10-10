using System.Globalization;
using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// An <see cref="IDatagramConnector"/> that emulates upstream's TFTP test server, <c>tftpd</c>
/// (<c>tests/server/tftpd.c</c> at <c>curl-8_21_0</c>), on <see cref="TftpPort"/>; an open to any
/// other port fails as <see cref="UnreachableDatagramConnector"/>'s does. No socket is opened.
/// </summary>
/// <remarks>
/// Each open is a <see cref="TftpServerChannel"/>. <see cref="ProtocolLog"/> holds the lines tftpd
/// writes to its <c>server.input</c> dump for each read or write request, across channels, for
/// comparison with <c>&lt;verify&gt;&lt;protocol&gt;</c>.
/// </remarks>
/// <param name="testCase">The test case, parsed after <see cref="UpstreamTestFileExpander"/> has expanded it.</param>
/// <param name="timeProvider">The clock <c>writedelay</c> waits on.</param>
public sealed class TftpServerConnector(UpstreamTestCase testCase, TimeProvider timeProvider) : IDatagramConnector
{
    /// <summary>The port of upstream's TFTP server, <c>%TFTPPORT</c>.</summary>
    public const int TftpPort = 9003;

    /// <summary>The port tftpd answers from, its transfer identifier.</summary>
    public const int TransferPort = 9004;

    private readonly MemoryStream log = new();

    /// <summary>The <c>server.input</c> dump: <c>opcode</c>, <c>mode</c>, each option and <c>filename</c>, one <c>name = value</c> line each, per request.</summary>
    public ReadOnlyMemory<byte> ProtocolLog => log.ToArray();

    /// <summary>The seconds tftpd waits before each DATA packet, from <c>&lt;servercmd&gt;</c>'s <c>writedelay: N</c>.</summary>
    internal int WriteDelaySeconds { get; } = ReadWriteDelay(testCase);

    internal TimeProvider TimeProvider => timeProvider;

    /// <summary>Opens a channel to the tftpd emulation when <paramref name="port"/> is <see cref="TftpPort"/>.</summary>
    /// <param name="host">The host from the URL; an IP literal is the server's address, any other name loopback.</param>
    /// <param name="port">The UDP port.</param>
    /// <param name="cancellationToken">Passed on.</param>
    /// <returns>The open channel, or the failure <see cref="UnreachableDatagramConnector"/> returns.</returns>
    public ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken) =>
        port == TftpPort
            ? ValueTask.FromResult(DatagramOpenResult.Opened(new TftpServerChannel(this, IPAddress.TryParse(host, out IPAddress? address) ? address : IPAddress.Loopback)))
            : new UnreachableDatagramConnector().OpenAsync(host, port, cancellationToken);

    /// <summary>Appends one <c>name = value</c> line to the dump.</summary>
    internal void Log(string name, string value) => log.Write(Encoding.Latin1.GetBytes($"{name} = {value}\n"));

    /// <summary>
    /// The file tftpd serves for <paramref name="filename"/>, or <see langword="null"/> for its
    /// access violation: the number after the last slash (non-digits skipped) picks
    /// <c>&lt;reply&gt;&lt;data&gt;</c>, or <c>&lt;dataN&gt;</c> for a number over 10000 whose
    /// last four digits are N; a part the case lacks is empty, as getpart reads it.
    /// </summary>
    internal byte[]? FindFile(string filename)
    {
        if (!int.TryParse(NumberAfterLastSlash(filename), NumberStyles.None, CultureInfo.InvariantCulture, out int number))
        {
            return null;
        }

        return testCase.Find("reply", DataPartName(number)) is { } part
            ? UpstreamTestPartBodies.WithoutFinalNewline(UpstreamTestPartBodies.Decoded(part), part)
            : [];
    }

    // The first run of digits after the last slash, or empty when there is no slash or no digit.
    private static string NumberAfterLastSlash(string filename)
    {
        int slash = filename.LastIndexOf('/');
        return slash < 0 ? string.Empty : new([.. filename[(slash + 1)..].SkipWhile(c => !char.IsAsciiDigit(c)).TakeWhile(char.IsAsciiDigit)]);
    }

    // <dataN> for a number over 10000 whose last four digits N are not all zero, else <data>.
    private static string DataPartName(int number) =>
        number > 10000 && number % 10000 != 0 ? $"data{(number % 10000).ToString(CultureInfo.InvariantCulture)}" : "data";

    private static int ReadWriteDelay(UpstreamTestCase testCase) =>
        UpstreamTestPartBodies.Lines(testCase.Find("reply", "servercmd"))
            .Select(line => line.StartsWith("writedelay: ", StringComparison.Ordinal)
                && int.TryParse(line["writedelay: ".Length..].Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int seconds) ? seconds : 0)
            .LastOrDefault(seconds => seconds != 0);
}
