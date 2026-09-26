namespace Curl.Conformance;

/// <summary>
/// One upstream curl test file, parsed by <see cref="UpstreamTestCaseParser"/>: every part of
/// every section, in file order. Variables and <c>%if</c> blocks are still as written.
/// </summary>
public sealed class UpstreamTestCase
{
    /// <summary>Creates a test case from its parts.</summary>
    /// <param name="sections">Every part, in file order.</param>
    public UpstreamTestCase(IReadOnlyList<UpstreamTestSection> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);
        Sections = sections;
    }

    /// <summary>Every part of every section, in file order.</summary>
    public IReadOnlyList<UpstreamTestSection> Sections { get; }

    /// <summary>The first part with the given section and name, or <see langword="null"/> when there is none.</summary>
    /// <param name="section">The enclosing section, such as <c>verify</c>.</param>
    /// <param name="name">The part name, such as <c>stdout</c>.</param>
    /// <returns>The first matching part, or <see langword="null"/>.</returns>
    public UpstreamTestSection? Find(string section, string name) =>
        FindAll(section, name).FirstOrDefault();

    /// <summary>
    /// Every part with the given section and name, in file order; for parts a test file may
    /// repeat, such as <c>&lt;client&gt;&lt;file name=…&gt;</c>.
    /// </summary>
    /// <param name="section">The enclosing section, such as <c>client</c>.</param>
    /// <param name="name">The part name, such as <c>file</c>.</param>
    /// <returns>The matching parts; empty when there are none.</returns>
    public IEnumerable<UpstreamTestSection> FindAll(string section, string name) =>
        Sections.Where(part => part.Section == section && part.Name == name);
}
