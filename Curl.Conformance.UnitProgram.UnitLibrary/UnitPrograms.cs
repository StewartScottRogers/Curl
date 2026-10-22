namespace Curl.Conformance.UnitProgram;

/// <summary>
/// The ported unit programs by <see cref="IUnitProgram.ProgramName"/>, which the upstream case runner
/// looks a <c>unittest</c> case's program up in (ADR-0711). An explicit table, never a reflection scan,
/// since every production project is published native AOT.
/// </summary>
public sealed partial class UnitPrograms
{
    private readonly Dictionary<string, IUnitProgram> programsByName = new(StringComparer.Ordinal);

    /// <summary>Creates the table of <paramref name="programs"/>.</summary>
    /// <param name="programs">The ported programs, each with a name of its own.</param>
    /// <exception cref="ArgumentNullException"><paramref name="programs"/> or one of them is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Two programs have the same <see cref="IUnitProgram.ProgramName"/>.</exception>
    public UnitPrograms(IEnumerable<IUnitProgram> programs)
    {
        ArgumentNullException.ThrowIfNull(programs);
        foreach (IUnitProgram program in programs)
        {
            ArgumentNullException.ThrowIfNull(program, nameof(programs));
            if (!programsByName.TryAdd(program.ProgramName, program))
            {
                throw new ArgumentException($"Two programs are named {program.ProgramName}.", nameof(programs));
            }
        }
    }

    /// <summary>
    /// Every program ported so far: the programs of the seven area files <c>UnitPrograms.&lt;Area&gt;.cs</c>
    /// (BL-2842 to BL-2848), each port task appending to its own area's file only.
    /// A <c>unittest</c> case whose program is not here is skipped as having no port.
    /// </summary>
    public static UnitPrograms Ported { get; } = new(
    [
        .. HashesPrograms,
        .. Asn1Programs,
        .. UrlParsingPrograms,
        .. ToolParsersPrograms,
        .. HeadersPrograms,
        .. DnsPrograms,
        .. LimitsPrograms,
    ]);

    /// <summary>The names of the programs in the table, such as <c>unit1302</c>.</summary>
    public IReadOnlyCollection<string> ProgramNames => programsByName.Keys;

    /// <summary>Finds the program a case names.</summary>
    /// <param name="programName">The program's name, such as <c>unit1302</c> or <c>tool1394</c>.</param>
    /// <returns>The program, or <see langword="null"/> when it is not ported.</returns>
    public IUnitProgram? Find(string programName) => programsByName.GetValueOrDefault(programName);
}
