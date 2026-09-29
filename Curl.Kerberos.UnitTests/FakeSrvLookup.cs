namespace Curl.Kerberos;

/// <summary>An <see cref="IKerberosSrvLookup" /> over a dictionary of names, so no test touches DNS.</summary>
internal sealed class FakeSrvLookup : IKerberosSrvLookup
{
    private readonly Dictionary<string, KerberosSrvRecord[]> answers = new(StringComparer.Ordinal);

    public List<string> NamesLookedUp { get; } = [];

    public FakeSrvLookup Add(string name, params KerberosSrvRecord[] records)
    {
        answers[name] = records;
        return this;
    }

    public Task<IReadOnlyList<KerberosSrvRecord>> LookUpAsync(string name, CancellationToken cancellationToken)
    {
        NamesLookedUp.Add(name);
        return Task.FromResult<IReadOnlyList<KerberosSrvRecord>>(answers.GetValueOrDefault(name) ?? []);
    }
}
