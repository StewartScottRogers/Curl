# ADR-0034 — HTTP authentication retries a 401 once, as curl 8.21.0 does

- **Status:** Accepted
- **Date:** 2026-09-26

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions").

## Context

BL-181 makes `HttpProtocolHandler` send the `Authorization` value `IHttpAuthenticator`
gives, and answer a 401's `WWW-Authenticate` challenges. The reference build (ADR-0018:
curl 8.21.0, mingw) was run against a loopback server that serves scripted responses on
one keep-alive connection (BL-181 Notes). What it did:

| Run | curl 8.21.0 |
| --- | --- |
| `-u u:p`, 200 | Sends `Authorization: Basic dTpw` straight after `Host`, before `User-Agent`. |
| `-u u:p`, 401 with a Basic challenge | No second request; exit 0, the 401's body is the output. |
| `-f -u u:p`, 401 | Exit 22, `The requested URL returned error: 401`. |
| `--digest -u u:p`, keep-alive 401 then 200 | A second request with Digest on the same connection; both heads written, only the 200's body; `%{num_connects}` 1, `%{size_request}` and `%{size_header}` add both exchanges. |
| The same, 401 carries `Connection: close` | The retry goes out on a new connection; `%{num_connects}` 2. |
| `--digest`, 401 then 401 | No third request; exit 0 with the second 401's body. Under `-f`, exit 22 on the second 401 only. |
| `--digest`, 401 with no `WWW-Authenticate` | No retry; exit 0 with the 401. |
| `--anyauth -d hello`, Basic challenge | The body is sent with both requests. |

No run produced exit 94 (`CURLE_AUTH_ERROR`): a refused credential is an HTTP result, not
a transfer error.

## Decision

- The first request carries the authenticator's answer to no challenges, placed after
  `Host`; an `-H Authorization` value replaces it in the custom headers' place.
- A 401 is retried once, and only when the request that drew it sent no `Authorization`,
  the response has at least one `WWW-Authenticate` value, and the authenticator answers
  them. The 401's head and trailers are written and its body read and discarded.
- The retry is sent on the same connection when the 401 leaves it open
  (`HttpConnectionPersistence`: not `Connection: close`, HTTP/1.1 or keep-alive, a framed
  body), otherwise on a new connection to the same target.
- A 401 that is not retried is the result: exit 0, or exit 22 under `-f`. The 401 a retry
  answers never fails the transfer.
- A request whose body is a stream (`-T`, `--data-binary @file` read as a stream) is not
  retried: the stream has been read by the time the 401 arrives and cannot be sent again.
  curl would rewind a seekable file through its seek callback; Curl's `StreamBody` has no
  rewind contract, so the 401 is the result. This case was not measured.
- The report adds the 401 exchange's request and header sizes and its connection to the
  final one, as `%{size_request}`, `%{size_header}` and `%{num_connects}` measured.

## Consequences

- Basic, Digest and the other schemes the authenticator answers behave as the reference
  does for every measured case, byte for byte on the wire and in the report.
- An upload from a stream that meets a challenge ends at the 401 where curl may retry it.
  A script that uploads a file with `--digest` sees a 401 instead of the upload; revisit
  if `StreamBody` gains a rewind.

## Alternatives considered

- **Return exit 94 for a refused credential.** Not what the reference does; measured exit 0.
- **Retry on every 401.** curl stops after the first refusal of a credential it sent; a
  loop would send more requests than the reference.
- **Buffer stream bodies to replay them.** Unbounded memory for large uploads, to cover an
  unmeasured case.
