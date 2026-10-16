# ADR-0650 — `CurlHttpMessageHandler` maps `SocketsHttpHandler`'s decompression, redirect, credential and cookie properties onto the easy options

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2512.

## Context

Code written against `HttpClient` sets `AutomaticDecompression`, `AllowAutoRedirect`,
`MaxAutomaticRedirections`, `Credentials`, `PreAuthenticate`, `UseCookies` and `CookieContainer` on
its handler. `CurlHttpMessageHandler` (ADR-0647) has to offer them with `SocketsHttpHandler`'s
defaults and map each onto the libcurl option that does the same, keeping the engine's answer where
the two differ.

## Decision

- **Defaults are `SocketsHttpHandler`'s:** no decompression, redirects followed up to 50, no
  credentials, no pre-authentication, cookies on with an empty container. So a handler now follows
  redirects even when the client's `CurlHttpOptions.FollowLocation` is off: the handler's properties
  win over the client's for the options they map.
- **Decompression:** `CURLOPT_ACCEPT_ENCODING` lists the codings named, `gzip, deflate, br` in
  `SocketsHttpHandler`'s order, with content decoding on. The engine decodes every coding it knows in
  such a response, so the response's whole `Content-Encoding` and its `Content-Length` are removed.
  `None` keeps the client's `AcceptEncoding` and the headers.
- **Redirects:** `CURLFOLLOW_OBEYCODE`, not `CURLFOLLOW_ALL`: like `SocketsHttpHandler` it turns any
  method but `HEAD` into `GET` after 303, as well as `POST` after 301 and 302.
  `MaxAutomaticRedirections` is `CURLOPT_MAXREDIRS`. Exceeding it fails the transfer with
  `CURLE_TOO_MANY_REDIRECTS`, thrown as `HttpRequestException`; `SocketsHttpHandler` returns the last
  3xx instead, but the engine has no such response to give. A followed redirect's URL
  (`CURLINFO_EFFECTIVE_URL`) becomes the request's `RequestUri`, as `SocketsHttpHandler` does. The
  engine also follows an HTTPS to HTTP redirect, which `SocketsHttpHandler` refuses; curl's answer stands.
- **Credentials:** libcurl takes one user and password, so the handler asks the credentials for
  Negotiate, NTLM, Digest and Basic, strongest first, takes the first credential given, and enables
  each scheme that gives that same credential (`CURLOPT_HTTPAUTH`). A domain goes as `domain\user`.
  A credential with no user name (`CredentialCache.DefaultCredentials`) is the logged-on user's and
  serves Negotiate and NTLM only. The client's other authentication members are kept, its
  `CURLOPT_USERPWD` cleared.
- **PreAuthenticate:** `false` adds `CURLAUTH_ONLY`, so even a Basic-only request first goes without
  credentials and answers the `401`, as `SocketsHttpHandler` does; `true` sends Basic at once.
- **Cookies:** the container's `Cookie` header for the URL goes as `CURLOPT_COOKIE`, and the final
  response's `Set-Cookie` fields are stored for its URL, one the container refuses skipped. A followed
  redirect's own `Set-Cookie` fields never reach the handler, so they are not stored; turning on the
  engine's cookie engine for them would mix with the client's shared cookie store (ADR-0645).

## Consequences

The handler behaves as `SocketsHttpHandler` for these seven properties apart from the three
differences named above (redirect limit, HTTPS to HTTP redirects, redirect cookies), each stated in
its property's doc comment. Tests drive an `HttpClient` over the scripted engine fakes and assert the
easy options the engine receives and the response returned.

`Credentials` was first added by ADR-0649 (BL-2511) as the `Basic` credential on the client's
methods; this decision supersedes that part. The handler has the one `Credentials` property, in
`HttpMessageHandler/Behaviour/`, applied before `HandlerProxyMapping`, which now puts only the proxy
and its credentials on the request, building on the request's authentication so the server
credential chosen here survives.
