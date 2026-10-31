namespace Curl.Protobuf.Descriptors;

/// <summary>One option, its name and value both the source text as written.</summary>
/// <param name="Name">The option's name: <c>java_package</c>, <c>(my.option).field</c>.</param>
/// <param name="Value">The option's value: <c>"com.example"</c> with its quotes, <c>-5</c>, <c>true</c>, a whole <c>{ ... }</c>.</param>
public sealed record ProtoOption(string Name, string Value);
