namespace Curl.Conformance.Extended.Plugin;

/// <summary>One program curl asked <see cref="PluginReferenceHelperStarter" /> to start.</summary>
/// <param name="Program">The program's path.</param>
/// <param name="Arguments">Its arguments.</param>
public sealed record PluginHelperStart(string Program, IReadOnlyList<string> Arguments);
