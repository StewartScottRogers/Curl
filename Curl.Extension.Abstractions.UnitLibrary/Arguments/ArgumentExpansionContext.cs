namespace Curl.Extension.Abstractions.Arguments;

/// <summary>
/// What an <see cref="IArgumentExpander" /> reads through the host (ADR-1040), so its tests need no disk and no
/// process environment.
/// </summary>
/// <param name="ReadEnvironmentVariable">Gives an environment variable's value, or <see langword="null" /> when it is not set.</param>
/// <param name="ReadFile">Gives every byte of the file at a path, or <see langword="null" /> when it cannot be read.</param>
public sealed record ArgumentExpansionContext(Func<string, string?> ReadEnvironmentVariable, Func<string, byte[]?> ReadFile);
