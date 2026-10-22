using Curl.Api.Codes;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.Options.Http;
using Libcurl.Global;

namespace Libcurl.Mime;

/// <summary>
/// A form built with libcurl's deprecated form API (ADR-0586): upstream's <c>httppost</c> and
/// <c>last_post</c> pointers, as one handle over the engine's <see cref="ICurlFormBuilder"/>.
/// <see cref="Add"/> is <c>curl_formadd</c>, <see cref="Get"/> <c>curl_formget</c> and
/// <see cref="Free"/> (or <see cref="Dispose"/>) <c>curl_formfree</c>; the form is sent with
/// <c>CURLOPT_HTTPPOST</c>. After <c>curl_formfree</c> every member but <see cref="IsFreed"/> throws
/// <see cref="ObjectDisposedException"/>.
/// </summary>
[Obsolete("curl_formadd, curl_formget and curl_formfree are deprecated since curl 7.56.0: use CurlLibrary.MimeInit (curl_mime_init).")]
public sealed class CurlForm : IDisposable
{
    private readonly ICurlFormBuilder builder;
    private bool freed;

    /// <summary>Wraps <paramref name="builder"/>.</summary>
    /// <param name="builder">The engine's form builder.</param>
    internal CurlForm(ICurlFormBuilder builder) => this.builder = builder;

    /// <summary>Whether <c>curl_formfree</c> has run.</summary>
    public bool IsFreed => freed;

    /// <summary>The engine's builder, after checking the form has not been freed.</summary>
    /// <exception cref="ObjectDisposedException">The form has been freed.</exception>
    private ICurlFormBuilder Builder
    {
        get
        {
            ObjectDisposedException.ThrowIf(freed, this);
            return builder;
        }
    }

    /// <summary>
    /// <c>curl_formadd</c>: adds one field, described by its <c>CURLFORM_*</c> options, each passed to the
    /// engine as given; <c>CURLFORM_END</c> is implied. A <see langword="null"/> <paramref name="form"/>
    /// starts a new form over a new engine form builder, which <paramref name="form"/> is set to only when
    /// the call succeeds, as upstream leaves <c>httppost</c> <c>NULL</c> after a failed first call.
    /// </summary>
    /// <param name="library">The library whose engine builds a new form.</param>
    /// <param name="form"><c>httppost</c> and <c>last_post</c>: the form to add to, or <see langword="null"/> to start one.</param>
    /// <param name="arguments">The field's options and values in order.</param>
    /// <returns>The engine's answer; the argument checks are the engine's.</returns>
    /// <exception cref="ObjectDisposedException">The library has been cleaned up, or <paramref name="form"/> freed.</exception>
    public static CurlFormAddCode Add(CurlLibrary library, ref CurlForm? form, params CurlFormArgument[] arguments)
    {
        var target = form ?? new CurlForm(library.CreateFormBuilder());
        var code = target.Builder.Add(arguments);
        if (code == CurlFormAddCode.Ok)
        {
            form = target;
        }

        return code;
    }

    /// <summary><c>curl_formfree</c>: frees <paramref name="form"/>; <see langword="null"/> or a freed form does nothing.</summary>
    /// <param name="form">The form, or <see langword="null"/>.</param>
    public static void Free(CurlForm? form) => form?.Dispose();

    /// <summary><c>curl_formget</c>: serialises the form as it would be sent, piece by piece.</summary>
    /// <param name="append">Receives each piece; <c>arg</c> is its closure.</param>
    /// <returns>0 when the whole form was serialised, else the engine's nonzero <c>CURLcode</c> number.</returns>
    /// <exception cref="ObjectDisposedException">The form has been freed.</exception>
    public int Get(FormGetCallback append) => Builder.Serialize(append);

    /// <summary><c>curl_formfree</c>: frees the form. A second call does nothing.</summary>
    public void Dispose() => freed = true;

    /// <summary>The body the added fields make, which <c>CURLOPT_HTTPPOST</c> sends.</summary>
    /// <returns>The body.</returns>
    /// <exception cref="ObjectDisposedException">The form has been freed.</exception>
    internal EasyMimeBody Build() => Builder.Build();
}
