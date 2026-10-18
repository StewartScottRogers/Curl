using Curl.Api.Codes;
using Curl.Api.Options;

namespace Libcurl.Options;

/// <summary>
/// <c>curl_easy_setopt</c>'s dispatch: looks the option up in the options table, checks the value's
/// type against the option's <see cref="CurlOptionType"/>, and hands it to the option group's setter.
/// </summary>
/// <param name="setters">The setters to dispatch through, by option.</param>
internal sealed class EasyOptionDispatch(IReadOnlyDictionary<CurlOption, EasyOptionSetter> setters)
{
    /// <summary>Upstream's <c>CURL_MAX_INPUT_LENGTH</c>: the longest string option <c>Curl_setstropt</c> copies.</summary>
    internal const int MaximumInputLength = 8_000_000;

    /// <summary>The dispatch over every setter the option groups have added (<see cref="EasyOptionSetters"/>).</summary>
    internal static EasyOptionDispatch Default { get; } = new(EasyOptionSetters.Create());

    /// <summary>Sets <paramref name="option"/> to <paramref name="value"/> on <paramref name="options"/>.</summary>
    /// <param name="options">The handle's options, replaced by the changed copy on success.</param>
    /// <param name="option">The option to set.</param>
    /// <param name="value">The value to set it to.</param>
    /// <returns>
    /// <see cref="CurlCode.UnknownOption"/> for an option upstream's table lacks,
    /// <see cref="CurlCode.BadFunctionArgument"/> for a value of the wrong type or a string past <see cref="MaximumInputLength"/>,
    /// <see cref="CurlCode.NotBuiltIn"/> for an option no group sets yet, else the setter's answer.
    /// </returns>
    internal CurlCode SetOption(ref EasyOptions options, CurlOption option, object? value)
    {
        var entry = CurlOptions.ById(option);
        if (entry is null)
        {
            return CurlCode.UnknownOption;
        }

        if (IsRefused(entry.Type, value))
        {
            return CurlCode.BadFunctionArgument;
        }

        return setters.TryGetValue(option, out var setter) ? setter(ref options, value) : CurlCode.NotBuiltIn;
    }

    /// <summary>Whether <paramref name="value"/> is of the wrong type for <paramref name="type"/>, or a string past <see cref="MaximumInputLength"/>.</summary>
    /// <param name="type">The option's type.</param>
    /// <param name="value">The value given.</param>
    /// <returns><see langword="true"/> when <c>curl_easy_setopt</c> answers <see cref="CurlCode.BadFunctionArgument"/>.</returns>
    private static bool IsRefused(CurlOptionType type, object? value) =>
        !CurlOptionValues.Accepts(type, value)
        || (type == CurlOptionType.String && value is string text && text.Length > MaximumInputLength);
}
