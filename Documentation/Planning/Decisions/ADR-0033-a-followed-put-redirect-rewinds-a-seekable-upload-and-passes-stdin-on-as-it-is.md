# ADR-0033 — A followed PUT redirect rewinds a seekable upload and passes stdin on as it is

- **Status:** Accepted
- **Date:** 2026-09-26

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions").

## Context

Under `-L`, a 301, 302, 307 or 308 answer to a `-T` upload keeps the PUT, so the next hop
sends the upload again. `RedirectFollower` passed the next hop the same `Stream`, which
the first hop had already read to its end (BL-253). Standard input (`-T -`) cannot be
rewound, so what the next hop sends for it had to be measured.

Measured with the reference build (curl 8.21.0, mingw, Schannel) on 2026-09-26 against a
loopback server answering the first request with `<status> Location: /next`:

- `curl -sS -L --max-redirs 1 -T up.txt http://127.0.0.1:18203/a` on 301 sends `PUT /a`
  with `abc`, then `PUT /next` with `Content-Length: 3` and `abc`.
- `curl -sS -L --max-redirs 1 -T - http://127.0.0.1:18253/a` with `abc` on stdin sends
  `PUT /a` chunked with `abc`, then, on 301, 302, 307 and 308, `PUT /next` chunked with
  an empty body (`0\r\n\r\n`), and exits 0 with the last hop's body on stdout. On 303 the
  second request is a GET with no body.

## Decision

- Before the first hop, `RedirectFollower` records where a seekable upload stands. Before
  every followed hop that keeps the upload, it puts the stream back there, so each PUT
  hop sends the whole upload.
- A non-seekable upload is passed on untouched. The next hop reads it from where the last
  hop left it - its end - and so sends an empty body, as curl does. No error, no warning.

## Consequences

Drop-in for both measured cases, with no buffering of standard input. An upload whose
first hop did not read it to its end (a failed or cut-short hop) is not followed anyway,
since only a successful 3xx is followed.

## Alternatives considered

- **Buffer standard input so it can be re-sent.** Sends bytes curl does not send; not a
  drop-in, and unbounded memory for a large pipe.
- **Fail the redirect for a non-seekable upload.** curl does not fail; exit 0 is what
  scripts see.
