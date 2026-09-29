# ADR-0243 — `--aws-sigv4` signs in an authenticator that puts its headers in the `Authorization` slot

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-629.

## Context

ADR-0178 built `AwsSigV4Signer`; BL-629 wires it into transfers. curl 8.21.0 (mingw, Schannel)
was measured with `Record-CurlExchange.ps1` (BL-628 and BL-629 Notes):

- The signer's three lines (`Authorization`, `X-Amz-Date`, `x-amz-content-sha256`) go right
  after `Host`, before `User-Agent`; `-H` headers follow curl's own, as always.
- With `-L`, curl signs each hop again for that hop's path and query.
- `--aws-sigv4` replaces every other scheme. libcurl's `CURLOPT_AWS_SIGV4` sets the
  authentication to SigV4 alone, and the tool sets it after `CURLOPT_HTTPAUTH`.
- A request curl cannot sign (for example exit 3, `aws-sigv4: service missing in parameters
  and hostname`) fails after the connection is made, with nothing sent.
- `-v` prints `aws_sigv4: String to sign (enclosed in []) - [...]`,
  `aws_sigv4: Signature - ...` and `Server auth using AWS_SIGV4 with user '...'` after
  `using HTTP/1.x`, just before the request. An `-H` `Authorization` leaves the request
  unsigned, but the last line is still printed.
- `--aws-sigv4 ""` is accepted and signs as `aws:amz`.

## Decision

1. **Sign in an authenticator, not in the request options.** The HTTP handler asks its
   `IHttpAuthenticator` for every request it sends, each redirect hop included, so a
   per-request signature follows naturally. `Curl.Console`'s `AwsSigV4HttpAuthenticator`
   wraps the ranked authenticator (only the HTTP handler gets the wrapper). When
   `HttpAuthRequest.AwsSigV4` is set, it signs before any challenge and never answers one, so
   a 401 is the transfer's result. Every other request goes to the other schemes.
2. **What the signer sees.** `Curl.Protocol.Abstractions` gains `AwsSigV4Inputs`: the
   parameter, curl's `Host` value, the `-H` headers, the `-d` bytes (`BytesBody`), the `-T`
   size (-1 when unknown), whether the request is a GET or HEAD, and `--path-as-is`. The HTTP
   handler fills it on `HttpAuthRequest.AwsSigV4` from `HttpRequestOptions.AwsSigV4`, the
   framing and the context. It is never set for a proxy.
3. **Headers in the `Authorization` slot.** An authenticator's value may continue with whole
   header lines, each after a CRLF. The formatter writes the value where `Authorization`
   goes, so the extra lines land where curl puts them without a second contract. This is
   documented on `IHttpAuthenticator.CreateAuthorization`.
4. **Refusal.** The signer's failure is thrown as `HttpAuthenticationFailedException`. The
   handler now catches it on the first, pre-emptive request too. It holds the failure on the
   request plan and throws it inside the exchange, before any byte is sent, so the
   connection is made first, as curl makes it.
5. **Command line.** `--aws-sigv4 <value>` accepts any value, empty included. The last one
   wins, and `CommandLineOptions.AuthSchemes` is left alone: the handler signs whenever the
   value is set, which is what "replaces every other scheme" means.
6. **Clock.** The signer reads the time from an injected `TimeProvider`. `CurlComposition`
   passes `TimeProvider.System`; tests pass a stopped clock so they can pin the measured
   bytes.

## Consequences

- WebSocket and RTSP keep the ranked authenticator; `--aws-sigv4` signs HTTP(S) only.
- With `--oauth2-bearer` and no `-u`, nothing is signed; libcurl would sign with an empty
  key ID. That edge is left for a follow-up if it is ever measured to matter.

## Alternatives considered

- **A structured result (`Value` plus `ExtraHeaderLines`) or a new interface member.** More
  contract for one scheme, and the extra lines would still be written in the same slot.
  Rejected for the documented multi-line value.
- **Signing in `HttpRequestOptions` or in `Curl.Console` before the transfer.** The options
  are fixed per transfer, while curl signs every redirect hop for its own URL. Rejected.
- **Adding the signer's lines as `-H` headers.** They would land after `Accept`, not where
  curl puts them. Rejected.
