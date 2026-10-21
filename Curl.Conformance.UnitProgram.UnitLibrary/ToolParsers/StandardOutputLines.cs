using System.Text;

namespace Curl.Conformance.UnitProgram.ToolParsers;

/// <summary>
/// Writes a ToolParsers port's lines to its standard output as the C program's <c>puts</c> and
/// <c>curl_mprintf</c> write them: ASCII text, each line ended by a line feed alone.
/// </summary>
/// <param name="context">The program's context, whose <see cref="UnitProgramContext.StandardOutput"/> is written.</param>
public sealed class StandardOutputLines(UnitProgramContext context)
{
    /// <summary>Writes <paramref name="line"/> and a line feed.</summary>
    /// <param name="line">The line, without its line feed.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    /// <returns>A task that completes once the line is written.</returns>
    public async Task WriteLineAsync(string line, CancellationToken cancellationToken) =>
        await context.StandardOutput.WriteAsync(Encoding.ASCII.GetBytes(line + "\n"), cancellationToken).ConfigureAwait(false);
}
