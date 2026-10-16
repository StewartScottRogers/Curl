namespace Libcurl.Options;

/// <summary>The <c>EasyTransferOptions</c> part of <see cref="EasyOptionSetters"/> (ADR-0560).</summary>
internal static partial class EasyOptionSetters
{
    /// <summary>Adds the <c>EasyTransferOptions</c> rows, from <see cref="EasyTransferOptionSetters"/>.</summary>
    /// <param name="setters">The rows to add to.</param>
    static partial void AddTransferSetters(Dictionary<CurlOption, EasyOptionSetter> setters) => EasyTransferOptionSetters.AddTo(setters);
}
