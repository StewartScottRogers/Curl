namespace Curl.Conformance.Differential.Libcurl.Scenarios;

/// <summary>A URL handle from <see cref="ILibcurlScenarioTarget.CreateUrl"/>; disposing it is <c>curl_url_cleanup</c>.</summary>
public interface ILibcurlScenarioUrl : IDisposable
{
    /// <summary>Calls <c>curl_url_set(handle, part, value, flags)</c> and returns its <c>CURLUcode</c>.</summary>
    int Set(int part, string? value, int flags);

    /// <summary>Calls <c>curl_url_get(handle, part, &amp;value, flags)</c>, returning its <c>CURLUcode</c> and the part's bytes, or <see langword="null"/>.</summary>
    int Get(int part, int flags, out byte[]? value);
}
