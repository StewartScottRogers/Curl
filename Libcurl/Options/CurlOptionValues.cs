namespace Libcurl.Options;

/// <summary>Which .NET values each <see cref="CurlOptionType"/> accepts, as its doc comment states.</summary>
internal static class CurlOptionValues
{
    private static readonly Func<object?, bool>[] Accepters =
    [
        IsInteger,
        value => IsInteger(value) || value is Enum,
        IsInteger,
        IsNotDelegate,
        value => value is null or string,
        value => value is null or IEnumerable<string>,
        IsNotDelegate,
        value => value is null or byte[] or ReadOnlyMemory<byte>,
        value => value is null or Delegate,
    ];

    /// <summary>Whether <paramref name="value"/> is a value an option of <paramref name="type"/> takes.</summary>
    /// <param name="type">The option's type.</param>
    /// <param name="value">The value given to <c>SetOption</c>.</param>
    /// <returns><see langword="true"/> when the value has the .NET type <paramref name="type"/> takes.</returns>
    internal static bool Accepts(CurlOptionType type, object? value) => Accepters[(int)type](value);

    private static bool IsInteger(object? value) => value is long or int;

    private static bool IsNotDelegate(object? value) => value is not Delegate;
}
