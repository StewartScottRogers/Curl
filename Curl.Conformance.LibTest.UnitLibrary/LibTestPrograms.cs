namespace Curl.Conformance.LibTest;

/// <summary>
/// The ported libtest programs by <see cref="ILibTest.ProgramName"/>, which the upstream case runner
/// looks a case's <c>&lt;client&gt;&lt;tool&gt;</c> line up in. An explicit table, never a reflection
/// scan, since every production project is published native AOT.
/// </summary>
public sealed class LibTestPrograms
{
    private readonly Dictionary<string, ILibTest> programsByName = new(StringComparer.Ordinal);

    /// <summary>Creates the table of <paramref name="programs"/>.</summary>
    /// <param name="programs">The ported programs, each with a name of its own.</param>
    /// <exception cref="ArgumentNullException"><paramref name="programs"/> or one of them is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Two programs have the same <see cref="ILibTest.ProgramName"/>.</exception>
    public LibTestPrograms(IEnumerable<ILibTest> programs)
    {
        ArgumentNullException.ThrowIfNull(programs);
        foreach (ILibTest program in programs)
        {
            ArgumentNullException.ThrowIfNull(program, nameof(programs));
            if (!programsByName.TryAdd(program.ProgramName, program))
            {
                throw new ArgumentException($"Two programs are named {program.ProgramName}.", nameof(programs));
            }
        }
    }

    /// <summary>
    /// Every program ported so far: the table each port task appends its program to. Empty until the
    /// first port lands, so every lib case is skipped as not ported.
    /// </summary>
    public static LibTestPrograms Ported { get; } = new([]);

    /// <summary>The names of the programs in the table, such as <c>lib500</c>.</summary>
    public IReadOnlyCollection<string> ProgramNames => programsByName.Keys;

    /// <summary>Finds the program a case's <c>&lt;tool&gt;</c> line names.</summary>
    /// <param name="programName">The program's name, such as <c>lib500</c>.</param>
    /// <returns>The program, or <see langword="null"/> when it is not ported.</returns>
    public ILibTest? Find(string programName) => programsByName.GetValueOrDefault(programName);
}
