# ADR-0647 — `Curl.Api`'s `CurlHttpMessageHandler` maps `HttpRequestMessage` onto `CurlRequest` and `CurlResponse` back onto `HttpResponseMessage`

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2510.

## Context

Phase 4 of `Documentation/Planning/Curl-Api-And-libcurl.md` puts Curl's engine under `HttpClient`:
`new HttpClient(new CurlHttpMessageHandler(client))`. `CurlClient.SendAsync` already sends a
`CurlRequest` and returns a `CurlResponse` whose body streams (ADR-0632, ADR-0635), honours a
cancellation token (ADR-0639) and throws `CurlException` for a failed transfer. The handler has to
choose how each part of an `HttpRequestMessage` reaches the engine and how each part of the response
reaches `HttpResponseMessage`.

## Decision

- **The client is not owned.** The handler takes a `CurlClient` and never disposes it: disposing the
  client is asynchronous and `HttpMessageHandler.Dispose` is not, and the rules forbid blocking on a
  task. The caller disposes the client after the `HttpClient`.
- **URL:** the request's absolute `RequestUri` as `Uri.AbsoluteUri`; a missing or relative URI throws
  `InvalidOperationException`, as `SocketsHttpHandler` does.
- **Headers:** the request headers then the content headers, each name once with its values joined as
  `HttpClient` writes them (`HeaderStringValues.ToString()`), read without validation.
  `Content-Length` is dropped: the engine sends its own from the body's size.
- **Body:** every `HttpContent` (string, bytes, form, multipart, stream) is read as a stream
  (`ReadAsStreamAsync`) and sent with `CurlContent.FromStream`, with `Headers.ContentLength` as its
  length; an unknown length goes chunked as libcurl does. The content's own `Content-Type` (a
  multipart boundary included) is sent as a header, so a `MultipartFormDataContent` goes out as the
  bytes .NET formats rather than as `CURLOPT_MIMEPOST`.
- **Version:** `HttpRequestMessage.Version` and `VersionPolicy` become `CURLOPT_HTTP_VERSION` on the
  client's `CurlHttpOptions` (or a default record), the other HTTP options kept: 1.0 and 1.1 ask for
  that version (libcurl's default under `RequestVersionOrHigher`), 2.0 for `CURL_HTTP_VERSION_2_0`
  (`2_PRIOR_KNOWLEDGE` under `RequestVersionExact`), 3.0 for `CURL_HTTP_VERSION_3` (`3ONLY` under
  `RequestVersionExact`). `HttpClient`'s default 1.1 request therefore asks for HTTP/1.1, as
  `SocketsHttpHandler` sends it.
- **Response:** status, version and reason (an empty reason, as HTTP/2 gives, leaves .NET's own phrase);
  each header goes to `HttpResponseMessage.Headers`, or to the content's headers when the message
  headers refuse it as a content header, so the split follows .NET's own header table. The content is
  a `StreamContent` over the streamed body; the first read that finds the body's end copies the
  trailers into `TrailingHeaders`, as `SocketsHttpHandler` fills them only at the end.
- **Errors:** a `CurlException` becomes `HttpRequestException` with the exception inside and its
  message; cancellation stays `OperationCanceledException`, which `HttpClient` turns into
  `TaskCanceledException` (with a `TimeoutException` inside for `HttpClient.Timeout`).

## Consequences

Any `HttpClient` code can run on Curl's engine with no other change. The synchronous
`HttpMessageHandler.Send` is not overridden and throws `NotSupportedException`, as the base does.
`CURLOPT_MIMEPOST` stays reachable through `CurlClient` and `CurlMultipartContent` directly.
