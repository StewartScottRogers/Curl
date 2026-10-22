using Curl.Api.Callbacks;
using Curl.Conformance.UnitProgram.Hashes;
using Curl.Engine;
using Curl.Output;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance.UnitProgram.Dns.Easy;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit1652.c</c>: <c>Curl_infof</c> through <c>CURLOPT_DEBUGFUNCTION</c>
/// under <c>CURLOPT_VERBOSE</c>, here <see cref="ITransferEvents.ReportInfo" /> on the engine's
/// <c>DebugCallbackTransferEvents</c>. The C program's checks, in order: <c>Simple Test</c> and
/// <c>Simple Test 42 testing 43</c> arrive with a line feed; an empty line is one byte; a NULL <c>%s</c> arrives as
/// <c>(nil)</c>; 2045 <c>A</c>s arrive whole as 2046 bytes, still inside the 2048-byte buffer; 2049 <c>A</c>s, 2049
/// <c>A</c>s and a line feed, and 4095 <c>A</c>s each arrive cut to 2047 bytes, inside the buffer. Curl formats in
/// C# before it reports, so the printf format and <c>(nil)</c> are C-only: the port hands over the text curl's
/// printf makes of them and checks the delivered line. Each long line's length is written to standard error, as the
/// C program writes it.
/// </summary>
/// <param name="deliver">Reports one info line and gives the bytes the debug callback got; the engine's by default.</param>
public sealed class Unit1652InfoLineTruncation(Func<string, string> deliver) : IUnitProgram
{
    private const int BufferSize = 2048;

    /// <summary>Creates the port over the engine's debug-callback transfer events.</summary>
    public Unit1652InfoLineTruncation()
        : this(DeliverThroughTheEngine)
    {
    }

    /// <inheritdoc />
    public string ProgramName => "unit1652";

    /// <inheritdoc />
    public async Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        UnitChecks checks = new(context.StandardError, ProgramName);
        checks.FailUnless(deliver("Simple Test") == "Simple Test\n", "Simple string test");
        checks.FailUnless(deliver("Simple Test 42 testing 43") == "Simple Test 42 testing 43\n", "Format string");
        checks.FailUnless(deliver(string.Empty).Length == 1, "Empty string");
        checks.FailUnless(deliver("(nil)") == "(nil)\n", "Passing NULL as string");

        string fits = new('A', 2045);
        string output = await DeliverLongAsync(context, fits).ConfigureAwait(false);
        checks.FailUnless(output.Length == 2046, "No truncation of infof input");
        checks.FailUnless(output == fits + "\n", "No truncation of infof input");
        checks.FailUnless(output.Length < BufferSize, "No truncation of infof input");

        await CheckCutAsync(context, checks, new string('A', 2049), "Truncation of infof input 1").ConfigureAwait(false);
        await CheckCutAsync(context, checks, new string('A', 2049) + "\n", "Truncation of infof input 2").ConfigureAwait(false);
        await CheckCutAsync(context, checks, new string('A', 4095), "Truncation of infof input 3").ConfigureAwait(false);
        return checks.Result;
    }

    private async Task CheckCutAsync(UnitProgramContext context, UnitChecks checks, string input, string message)
    {
        string output = await DeliverLongAsync(context, input).ConfigureAwait(false);
        checks.FailUnless(output.Length == 2047, message);
        checks.FailUnless(output.Length < BufferSize, message);
    }

    private async Task<string> DeliverLongAsync(UnitProgramContext context, string input)
    {
        string output = deliver(input);
        await context.StandardError.WriteAsync($"output len {output.Length}: {output}").ConfigureAwait(false);
        return output;
    }

    private static string DeliverThroughTheEngine(string line)
    {
        string delivered = string.Empty;
        new DebugCallbackTransferEvents(Record, TlsBackend.Schannel).ReportInfo(line);
        return delivered;

        int Record(CurlInfoType type, ReadOnlySpan<byte> data)
        {
            delivered = System.Text.Encoding.UTF8.GetString(data);
            return 0;
        }
    }
}
