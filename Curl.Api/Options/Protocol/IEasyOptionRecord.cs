namespace Curl.Api;

/// <summary>A <c>Curl.Api</c> option record that maps whole onto one seam option group.</summary>
/// <typeparam name="TEasy">The seam's option group.</typeparam>
internal interface IEasyOptionRecord<out TEasy>
{
    /// <summary>A <typeparamref name="TEasy"/> holding every member of the record.</summary>
    /// <returns>The seam's options.</returns>
    TEasy ToEasy();
}
