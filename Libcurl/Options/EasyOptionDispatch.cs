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
    /// <summary>The dispatch over every setter the option groups have added (<see cref="EasyOptionSetters"/>).</summary>
    internal static EasyOptionDispatch Default { get; } = new(EasyOptionSetters.Create());

    /// <summary>Sets <paramref name="option"/> to <paramref name="value"/> on <paramref name="options"/>.</summary>
    /// <param name="options">The handle's options, replaced by the changed copy on success.</param>
    /// <param name="option">The option to set.</param>
    /// <param name="value">The value to set it to.</param>
    /// <returns>
    /// <see cref="CurlCode.UnknownOption"/> for an option upstream's table lacks,
    /// <see cref="CurlCode.BadFunctionArgument"/> for a value of the wrong type,
    /// <see cref="CurlCode.NotBuiltIn"/> for an option no group sets yet, else the setter's answer.
    /// </returns>
    internal CurlCode SetOption(ref EasyOptions options, CurlOption option, object? value)
    {
        var entry = CurlOptions.ById(option);
        if (entry is null)
        {
            return CurlCode.UnknownOption;
        }

        if (!CurlOptionValues.Accepts(entry.Type, value))
        {
            return CurlCode.BadFunctionArgument;
        }

        return setters.TryGetValue(option, out var setter) ? setter(ref options, value) : CurlCode.NotBuiltIn;
    }
}
