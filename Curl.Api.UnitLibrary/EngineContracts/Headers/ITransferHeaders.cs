using Curl.Api.Codes;

namespace Curl.Api.EngineContracts.Headers;

/// <summary>
/// libcurl's header API on one easy handle (<c>include/curl/header.h</c>): the headers its
/// transfers received, kept until the next transfer starts.
/// </summary>
public interface ITransferHeaders
{
    /// <summary>The <c>request</c> argument -1: the last request of the transfer, after any redirects.</summary>
    const int LastRequest = -1;

    /// <summary><c>curl_easy_header</c>: find one header by name.</summary>
    /// <param name="name">The header's name, matched without regard to case.</param>
    /// <param name="index">Which header of that name, from 0.</param>
    /// <param name="origin">The <c>CURLH_*</c> bits to search.</param>
    /// <param name="request">Which request of the transfer, from 0, or <see cref="LastRequest"/>.</param>
    /// <param name="header"><c>*hout</c>: the header, or <see langword="null"/> when the result is not <see cref="CurlHeaderCode.Ok"/>.</param>
    /// <returns>The lookup's result.</returns>
    CurlHeaderCode TryGet(string name, int index, CurlHeaderOrigin origin, int request, out CurlHeader? header);

    /// <summary><c>curl_easy_nextheader</c>: the header after <paramref name="previous"/>.</summary>
    /// <param name="origin">The <c>CURLH_*</c> bits to walk.</param>
    /// <param name="request">Which request of the transfer, from 0, or <see cref="LastRequest"/>.</param>
    /// <param name="previous"><c>prev</c>: the header returned last, or <see langword="null"/> for the first.</param>
    /// <returns>The next header, or <see langword="null"/> when there are no more.</returns>
    CurlHeader? Next(CurlHeaderOrigin origin, int request, CurlHeader? previous);
}
