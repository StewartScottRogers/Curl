namespace Curl.Kerberos;

/// <summary>
/// One node of a parsed <c>krb5.conf</c>: a section, a <c>{</c> group, or a
/// <c>tag = value</c> relation, as MIT's profile library holds it.
/// </summary>
/// <param name="name">The section name, group tag or relation tag, case-sensitive.</param>
/// <param name="value">The relation's value, or <see langword="null" /> for a section or group.</param>
public sealed class KerberosConfigurationNode(string name, string? value)
{
    private readonly List<KerberosConfigurationNode> children = [];

    /// <summary>Gets the section name, group tag or relation tag.</summary>
    public string Name { get; } = name;

    /// <summary>Gets the relation's value, or <see langword="null" /> for a section or group.</summary>
    public string? Value { get; } = value;

    /// <summary>Gets the relations and groups inside a section or group, in file order.</summary>
    public IReadOnlyList<KerberosConfigurationNode> Children => children;

    /// <summary>
    /// Gets the group named <paramref name="groupName" /> inside this one, adding it when
    /// there is none, so a section or group that appears twice holds the relations of both.
    /// </summary>
    internal KerberosConfigurationNode GetOrAddGroup(string groupName)
    {
        KerberosConfigurationNode? existing = children.Find(child => child.Value is null && child.Name == groupName);
        if (existing is not null)
        {
            return existing;
        }

        KerberosConfigurationNode added = new(groupName, null);
        children.Add(added);
        return added;
    }

    /// <summary>Adds the relation <paramref name="tag" /> = <paramref name="relationValue" />.</summary>
    internal void AddRelation(string tag, string relationValue) => children.Add(new KerberosConfigurationNode(tag, relationValue));
}
