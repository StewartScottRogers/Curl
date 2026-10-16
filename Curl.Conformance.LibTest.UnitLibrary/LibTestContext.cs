namespace Curl.Conformance.LibTest;

/// <summary>
/// What libtest's <c>first.c</c> hands a program's <c>test()</c>: the command-line arguments
/// after the program name, the case's test number (<c>testnum</c>) and its URL
/// (<c>argv[1]</c>, <c>URL</c>).
/// </summary>
public sealed class LibTestContext
{
    /// <summary>Creates the context for one run of a program.</summary>
    /// <param name="testNumber">The upstream case's number, such as 500; never negative.</param>
    /// <param name="arguments">
    /// The arguments after the program name, as the case's <c>&lt;command&gt;</c> line gives them;
    /// the first is the URL. Copied, so a list changed afterwards leaves the context as created.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="arguments"/> or one of them is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="testNumber"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="arguments"/> is empty, so there is no URL.</exception>
    public LibTestContext(int testNumber, IReadOnlyList<string> arguments)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(testNumber);
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Count == 0)
        {
            throw new ArgumentException("A libtest program needs at least its URL argument.", nameof(arguments));
        }

        string[] copy = [.. arguments];
        if (Array.IndexOf(copy, null) >= 0)
        {
            throw new ArgumentNullException(nameof(arguments), "A libtest argument cannot be null.");
        }

        TestNumber = testNumber;
        Arguments = copy;
    }

    /// <summary>The upstream case's number, libtest's <c>testnum</c>.</summary>
    public int TestNumber { get; }

    /// <summary>The arguments after the program name; <c>Arguments[0]</c> is <see cref="Url"/>.</summary>
    public IReadOnlyList<string> Arguments { get; }

    /// <summary>The case's URL, libtest's <c>URL</c> (<c>argv[1]</c>).</summary>
    public string Url => Arguments[0];

    /// <summary>The second argument, libtest's <c>libtest_arg2</c>, or <see langword="null"/> when not given.</summary>
    public string? Argument2 => ArgumentOrNull(1);

    /// <summary>The third argument, libtest's <c>libtest_arg3</c>, or <see langword="null"/> when not given.</summary>
    public string? Argument3 => ArgumentOrNull(2);

    /// <summary>The fourth argument, libtest's <c>libtest_arg4</c>, or <see langword="null"/> when not given.</summary>
    public string? Argument4 => ArgumentOrNull(3);

    private string? ArgumentOrNull(int index) => index < Arguments.Count ? Arguments[index] : null;
}
