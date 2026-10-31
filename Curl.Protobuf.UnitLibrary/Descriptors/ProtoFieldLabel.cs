namespace Curl.Protobuf.Descriptors;

/// <summary>How many values a field holds.</summary>
public enum ProtoFieldLabel
{
    /// <summary>No label: one value, absent when it is the default.</summary>
    Singular,

    /// <summary><c>optional</c>: one value whose presence is kept.</summary>
    Optional,

    /// <summary><c>repeated</c>, or a <c>map&lt;,&gt;</c> field: any number of values.</summary>
    Repeated,
}
