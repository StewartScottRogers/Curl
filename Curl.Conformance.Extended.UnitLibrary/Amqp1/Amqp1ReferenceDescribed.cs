namespace Curl.Conformance.Extended.Amqp1;

/// <summary>
/// A described value (Part 1 section 1.2) with a numeric descriptor, such as a performative or SASL frame body whose
/// value is the list of its fields.
/// </summary>
/// <param name="Descriptor">The descriptor code, such as <see cref="Amqp1ReferenceDescriptors.Open"/>.</param>
/// <param name="Value">The described value; for a composite type, the list of its fields.</param>
public sealed record Amqp1ReferenceDescribed(ulong Descriptor, object? Value)
{
    /// <summary>Gets field <paramref name="index"/> of a composite value; null when the list is shorter or the value is no list.</summary>
    /// <param name="index">The field's position, from 0.</param>
    /// <returns>The field's value.</returns>
    public object? Field(int index) => Value is IReadOnlyList<object?> fields && index < fields.Count ? fields[index] : null;
}
