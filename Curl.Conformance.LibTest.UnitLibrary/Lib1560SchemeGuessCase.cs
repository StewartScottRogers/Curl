using Curl.Api.EngineContracts.Url;

namespace Curl.Conformance.LibTest;

/// <summary>One row of <c>lib1560.c</c>'s <c>test_scheme_guess</c> table (<c>struct guessscheme1560</c>).</summary>
/// <param name="Url">The URL set.</param>
/// <param name="ExpectedFail">Whether the URL is wanted to fail to parse.</param>
/// <param name="SetFlags">The flags the URL is set with.</param>
/// <param name="Scheme">The scheme wanted back, or <see langword="null"/> for none.</param>
/// <param name="GetFlags">The flags the scheme is read with.</param>
public sealed record Lib1560SchemeGuessCase(string Url, bool ExpectedFail, CurlUrlFlags SetFlags, string? Scheme, CurlUrlFlags GetFlags);
