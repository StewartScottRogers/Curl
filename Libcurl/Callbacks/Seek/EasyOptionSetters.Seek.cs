using Libcurl.Callbacks.Seek;

namespace Libcurl.Options;

/// <summary>The seek part of <see cref="EasyOptionSetters"/> (ADR-0573): the two ways to rewind an upload.</summary>
internal static partial class EasyOptionSetters
{
    /// <summary>Adds the seek rows, from <see cref="EasySeekCallbackSetters"/>, and the ioctl rows, from <see cref="EasyIoctlCallbackSetters"/>.</summary>
    /// <param name="setters">The rows to add to.</param>
    static partial void AddSeekCallbackSetters(Dictionary<CurlOption, EasyOptionSetter> setters)
    {
        EasySeekCallbackSetters.AddTo(setters);
        EasyIoctlCallbackSetters.AddTo(setters);
    }
}
