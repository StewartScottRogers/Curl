namespace Curl.Api.Options.Http;

/// <summary>
/// <c>CURLOPT_STREAM_DEPENDS</c> or <c>CURLOPT_STREAM_DEPENDS_E</c>: the HTTP/2 stream this
/// transfer's stream depends on.
/// </summary>
/// <param name="Parent">
/// The easy handle whose stream is the parent. Typed <see cref="object"/> because the
/// handle type belongs to the API built on this seam (ADR-0508).
/// </param>
/// <param name="Exclusive">
/// <see langword="true"/> for <c>CURLOPT_STREAM_DEPENDS_E</c>: this stream becomes the
/// parent's only dependant; <see langword="false"/> for <c>CURLOPT_STREAM_DEPENDS</c>.
/// </param>
public sealed record EasyStreamDependency(object Parent, bool Exclusive);
