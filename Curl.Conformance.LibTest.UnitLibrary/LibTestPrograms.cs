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
    /// Every program ported so far, over the system clock: the table each port task appends its program to.
    /// A lib case whose program is not here is skipped as not ported.
    /// </summary>
    public static LibTestPrograms Ported { get; } = new(
    [
        new Lib500(TimeProvider.System),
        new Lib501(),
        new Lib502(TimeProvider.System),
        new Lib503(TimeProvider.System),
        new Lib504(TimeProvider.System),
        new Lib505(),
        new Lib506(),
        new Lib507(TimeProvider.System),
        new Lib508(),
        new Lib509(() => new LibTestCountingAllocator()),
        new Lib510(),
        new Lib511(),
        new Lib512(),
        new Lib513(),
        new Lib514(),
        new Lib515(),
        new Lib516(),
        new Lib517(),
        new Lib518(),
        new Lib519(),
        LibOneVerboseTransfer.Filetime,
        new Lib521(),
        LibOneVerboseTransfer.ProxyPortUser,
        LibOneVerboseTransfer.Upload,
        new Lib525(TimeProvider.System),
        new Lib526(TimeProvider.System),
        new Lib530(TimeProvider.System),
        new Lib533(TimeProvider.System),
        new Lib536(),
        new Lib537(),
        new Lib539(),
        new Lib540(TimeProvider.System),
        new Lib541(),
        LibOneVerboseTransfer.NobodyWithoutHeader,
        new Lib543(),
        new Lib544(),
        new Lib547(),
        LibOneVerboseTransfer.ProxyTransferMode,
        new Lib552(TimeProvider.System),
        new Lib553(),
    ]);

    /// <summary>The names of the programs in the table, such as <c>lib500</c>.</summary>
    public IReadOnlyCollection<string> ProgramNames => programsByName.Keys;

    /// <summary>Finds the program a case's <c>&lt;tool&gt;</c> line names.</summary>
    /// <param name="programName">The program's name, such as <c>lib500</c>.</param>
    /// <returns>The program, or <see langword="null"/> when it is not ported.</returns>
    public ILibTest? Find(string programName) => programsByName.GetValueOrDefault(programName);
}
