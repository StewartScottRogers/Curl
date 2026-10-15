namespace Curl.Api.EngineContracts;

/// <summary>
/// A transfer engine behind <c>Curl.Api</c> and <c>Libcurl</c> (ADR-0506, "Layering"). Its
/// lifetime is <c>curl_global_init</c>'s to <c>curl_global_cleanup</c>'s: constructing it is the
/// init, <see cref="IAsyncDisposable.DisposeAsync"/> is the cleanup.
/// </summary>
public interface ICurlEngine : IAsyncDisposable
{
    /// <summary><c>curl_version_info</c>: the engine's version, features, protocols and TLS backend.</summary>
    CurlVersionInfo VersionInfo { get; }

    /// <summary><c>curl_easy_init</c>: a new easy handle with every option at its default.</summary>
    /// <returns>The new easy handle; the caller disposes it (<c>curl_easy_cleanup</c>).</returns>
    IEasyTransfer CreateEasy();

    /// <summary><c>curl_multi_init</c>: a new multi handle with every option at its default.</summary>
    /// <returns>The new multi handle; the caller disposes it (<c>curl_multi_cleanup</c>).</returns>
    Multi.IMultiTransfer CreateMulti();

    /// <summary><c>curl_share_init</c>: a new share handle that shares nothing yet.</summary>
    /// <returns>The new share handle; the caller frees it with <see cref="Share.IShareHandle.Cleanup"/>.</returns>
    Share.IShareHandle CreateShare();

    /// <summary><c>curl_url</c>: a new URL handle with every part unset.</summary>
    /// <returns>The new URL handle; the caller disposes it (<c>curl_url_cleanup</c>).</returns>
    Url.ICurlUrlHandle CreateUrl();

    /// <summary><c>curl_mime_init</c>: a new MIME handle with no parts.</summary>
    /// <returns>The new MIME handle.</returns>
    Mime.IMimeBuilder CreateMime();

    /// <summary>A new, empty form for <c>curl_formadd</c>, whose first call starts from a null <c>httppost</c>.</summary>
    /// <returns>The new form builder.</returns>
    Mime.ICurlFormBuilder CreateForm();
}
