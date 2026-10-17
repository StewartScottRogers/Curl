using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Options.Authentication;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib552.c</c>: a POST of 70000 bytes of repeated <c>test data</c> and its NUL from a read callback through
/// the proxy <c>libtest_arg2</c>, any proxy authentication, traced by <c>libtest_debug_cb</c> with no hex and no
/// time, the response printed to stdout as <c>%.*s</c> prints it (up to a NUL), and rewound by a
/// <c>CURLOPT_IOCTLFUNCTION</c> that says so on stdout (<see cref="DataBuffer"/>).
/// </summary>
/// <param name="timeProvider">The trace's clock; the trace writes no time.</param>
public sealed class Lib552(TimeProvider timeProvider) : ILibTest
{
    /// <summary>How many bytes the program posts.</summary>
    public const int DataLength = 70000;

    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib552";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, timeProvider, cancellationToken));

    /// <summary>The program's body after <c>global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="timeProvider">The trace's clock.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes when the program reaches its cleanup.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.EasyInit(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            var trace = new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: false, noHex: true);
            var data = new DataBuffer(context.StandardOutput);
            await steps.SetOptionsAndPerformAsync(
                easy,
                [
                    (CurlOption.Debugfunction, (DebugCallback)trace.Trace),
                    (CurlOption.Debugdata, trace),
                    (CurlOption.Verbose, 1L),
                    (CurlOption.Post, 1L),
                    (CurlOption.Postfieldsize, (long)DataLength),
                    (CurlOption.Readfunction, (ReadCallback)data.Read),
                    (CurlOption.Writefunction, (WriteCallback)data.Write),
                    (CurlOption.Ioctlfunction, (Func<int, int>)data.Ioctl),
                    (CurlOption.Proxy, context.Argument2),
                    (CurlOption.Url, context.Url),
                    (CurlOption.Proxyauth, (long)HttpAuthenticationMethods.Any),
                ],
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary><c>databuf</c> with its read, write and ioctl callbacks.</summary>
    /// <param name="standardOutput">Where the write and ioctl callbacks print.</param>
    public sealed class DataBuffer(Stream standardOutput)
    {
        private static readonly byte[] Data = BuildData();

        private int offset;

        /// <summary><c>t552_read_cb</c>: the next bytes of the data, as many as fit.</summary>
        /// <param name="buffer">Where the bytes go.</param>
        /// <returns>How many bytes were given; 0 at the end.</returns>
        public long Read(Span<byte> buffer)
        {
            int given = Math.Min(buffer.Length, Data.Length - offset);
            Data.AsSpan(offset, given).CopyTo(buffer);
            offset += given;
            return given;
        }

        /// <summary><c>t552_write_cb</c>: prints the bytes up to the first NUL, as <c>%.*s</c> does, and takes them all.</summary>
        /// <param name="data">The received bytes.</param>
        /// <returns>The number of bytes received.</returns>
        public long Write(ReadOnlySpan<byte> data)
        {
            int end = data.IndexOf((byte)0);
            standardOutput.Write(end < 0 ? data : data[..end]);
            return data.Length;
        }

        /// <summary><c>ioctl_callback</c>: on <see cref="LibTestIoctl.RestartRead"/>, says so on stdout and rewinds.</summary>
        /// <param name="command">The command libcurl passes.</param>
        /// <returns><see cref="LibTestIoctl.Ok"/> for a rewind, else <see cref="LibTestIoctl.UnknownCommand"/>.</returns>
        public int Ioctl(int command)
        {
            if (command != LibTestIoctl.RestartRead)
            {
                return LibTestIoctl.UnknownCommand;
            }

            standardOutput.Write("APPLICATION received a CURLIOCMD_RESTARTREAD request\nAPPLICATION ** REWINDING! **\n"u8);
            offset = 0;
            return LibTestIoctl.Ok;
        }

        private static byte[] BuildData()
        {
            byte[] fill = Encoding.ASCII.GetBytes("test data\0");
            byte[] data = new byte[DataLength];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = fill[i % fill.Length];
            }

            return data;
        }
    }
}
