namespace Curl.Kerberos;

/// <summary>
/// The contents of an MIT keytab file: its keys, in file order. <see cref="Dispose" />
/// zeroes every key.
/// </summary>
/// <param name="entries">The entries, in file order, holes left out.</param>
public sealed class Keytab(IReadOnlyList<KeytabEntry> entries) : IDisposable
{
    /// <summary>Gets the entries, in file order, holes left out.</summary>
    public IReadOnlyList<KeytabEntry> Entries { get; } = entries;

    /// <summary>Zeroes every entry's key.</summary>
    public void Dispose()
    {
        foreach (KeytabEntry entry in Entries)
        {
            entry.Key.Dispose();
        }
    }
}
