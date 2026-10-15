namespace Curl.Api.Callbacks;

/// <summary>
/// <c>CURLOPT_SEEKFUNCTION</c>: moves the upload source to a new position, to resend data.
/// <c>CURLOPT_SEEKDATA</c> is the delegate's closure.
/// </summary>
/// <param name="offset">The position to move to, relative to <paramref name="origin"/>.</param>
/// <param name="origin">
/// Where <paramref name="offset"/> counts from; <see cref="SeekOrigin"/>'s numbers are
/// <c>SEEK_SET</c>, <c>SEEK_CUR</c> and <c>SEEK_END</c>'s. libcurl only passes <see cref="SeekOrigin.Begin"/>.
/// </param>
/// <returns>Whether the seek worked.</returns>
public delegate SeekResult SeekCallback(long offset, SeekOrigin origin);
