using System.Globalization;
using System.Text;
using Curl.Api.Callbacks;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib500.c</c>: a plain GET of the URL with headers and a <c>libtest_debug_cb</c> trace (no hex, with
/// times), <c>CURLOPT_FTPPORT</c> <c>-</c> when the third argument is <c>activeftp</c>, and the open and
/// close socket callbacks for cases 585, 586, 595 and 596, which write <c>[OPEN] counter: N</c> and
/// <c>[CLOSE] counter: N</c> to <c>stdout</c>. After a successful transfer it reads
/// <c>CURLINFO_PRIMARY_IP</c> and, when a second argument names a file, writes <c>IP &lt;address&gt;</c>
/// there, then one line for each pair of the transfer's times that is out of order.
/// </summary>
/// <param name="timeProvider">Where the trace's times come from.</param>
public sealed class Lib500(TimeProvider timeProvider) : ILibTest
{
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib500";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes when the program reaches its cleanup.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.EasyInitOrMajorBad(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            await using StreamWriter output = LibTestRun.StandardOutputWriter(context);
            var trace = new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true);
            if (await steps.SetOptionsAndPerformAsync(easy, OptionsOf(context, trace, output), cancellationToken).ConfigureAwait(false))
            {
                steps.EasyGetInfo(easy, CurlInfo.PrimaryIp, out object? address);
                WriteTimes(easy, context.Argument2, (string?)address);
            }
        }
    }

    private static List<(CurlOption Option, object? Value)> OptionsOf(LibTestContext context, LibTestDebugTrace trace, StreamWriter output)
    {
        List<(CurlOption Option, object? Value)> options =
        [
            (CurlOption.Writedata, context.StandardOutput),
            (CurlOption.Url, context.Url),
            (CurlOption.Header, 1L),
            (CurlOption.Debugdata, trace),
            (CurlOption.Debugfunction, (DebugCallback)trace.Trace),
            (CurlOption.Verbose, 1L),
        ];
        if (context.Argument3 == "activeftp")
        {
            options.Add((CurlOption.Ftpport, "-"));
        }

        if (context.TestNumber is 585 or 586 or 595 or 596)
        {
            var counter = new SocketCounter(output);
            options.Add((CurlOption.Opensocketfunction, (OpenSocketCallback)counter.Open));
            options.Add((CurlOption.Closesocketfunction, (CloseSocketCallback)counter.Close));
        }

        return options;
    }

    // Writes "IP <address>" and the out-of-order times to the file the second argument names, when it names one.
    private static void WriteTimes(CurlEasy easy, string? path, string? address)
    {
        if (path is null)
        {
            return;
        }

        using var file = new StreamWriter(path, append: false, new UTF8Encoding(false)) { NewLine = "\n" };
        file.WriteLine($"IP {address}");
        long nameLookup = TimeOf(easy, CurlInfo.NamelookupTimeT);
        long connect = TimeOf(easy, CurlInfo.ConnectTimeT);
        long preTransfer = TimeOf(easy, CurlInfo.PretransferTimeT);
        long postTransfer = TimeOf(easy, CurlInfo.PosttransferTimeT);
        long startTransfer = TimeOf(easy, CurlInfo.StarttransferTimeT);
        long total = TimeOf(easy, CurlInfo.TotalTimeT);
        WriteIfLater(file, "namelookup vs connect", nameLookup, connect);
        WriteIfLater(file, "connect vs pretransfer", connect, preTransfer);
        WriteIfLater(file, "pretransfer vs posttransfer", preTransfer, postTransfer);
        WriteIfLater(file, "pretransfer vs starttransfer", preTransfer, startTransfer);
        WriteIfLater(file, "starttransfer vs total", startTransfer, total);
        WriteIfLater(file, "posttransfer vs total", postTransfer, total);
    }

    private static long TimeOf(CurlEasy easy, CurlInfo info)
    {
        easy.GetInfo(info, out object? value);
        return Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    // The "%lld.%06ld %lld.%06ld" line upstream writes when the earlier time is the larger.
    private static void WriteIfLater(StreamWriter file, string label, long earlier, long later)
    {
        if (earlier > later)
        {
            file.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{label}: {earlier / 1000000}.{earlier % 1000000:D6} {later / 1000000}.{later % 1000000:D6}"));
        }
    }

    // tst_opensocket and tst_closesocket: the counter is the number of open sockets, each callback writing it to stdout.
    private sealed class SocketCounter(StreamWriter output)
    {
        private int counter;

        public CurlSocket Open(CurlSocketType purpose, CurlSocketAddress address)
        {
            counter++;
            output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"[OPEN] counter: {counter}"));
            return new CurlSocket(counter);
        }

        public int Close(CurlSocket socket)
        {
            output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"[CLOSE] counter: {counter}"));
            counter--;
            return 0;
        }
    }
}
