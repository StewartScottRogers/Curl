# ADR-0214 — `--alt-svc` gives each HTTP transfer its own cache and uses only `h1` alternatives

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-623.
Builds on ADR-0175 (`AltSvcCache`) and ADR-0208 (the route and store seams).

## Context

`--alt-svc <file>` has to read curl's cache file, pick the alternative a transfer connects to,
learn from `Alt-Svc` headers and write the file back. The measurements are in BL-623's Notes
(curl 8.21.0 mingw Schannel, 2026-09-29), plus these taken for this decision: `--alt-svc` warns
nothing for a value like `-abc` and accepts `""`; the file is written after an `http` or `https`
transfer, even one whose connect was refused, and not after a `file://` or `ftp://` one; a file
in a directory that does not exist is silently not written.

## Decision

1. **Per transfer.** Each `http`/`https` transfer gets a fresh `AltSvcTransferCache`
   (`Curl.Console`), reading the file before the transfer and writing it after, as libcurl gives
   each easy handle its own cache loaded at setup and saved at cleanup, and the tool makes one
   handle per transfer. `--alt-svc ""` learns into a cache that nothing reads or writes, so a later
   transfer knows nothing of an earlier one's headers. Other schemes neither read nor write it.
2. **Only `h1`.** The lookup is `Find(h1, host, port, {h1})`: the transfer speaks HTTP/1.1, and curl
   skips an alternative whose version it may not use. BL-733 widens this once HTTP/2 and HTTP/3 run.
   Every header is learned as having come over `h1`, from the origin (the request URL), never the
   alternative.
3. **When none is used.** Plain `http`, an origin a `--connect-to` mapping matches (or one that fails
   to parse), and an entry naming the origin itself (measured case 5) get no route, so no `Alt-Used`.
4. **Redirects.** `RedirectFollower` keeps the route only for a hop to the first URL's origin; a hop to
   another origin connects there directly. curl looks each hop up afresh; doing that needs a lookup on
   the store seam and is left to BL-733.
5. **Failures are silent.** A missing or unreadable file reads as empty; a file that cannot be
   written is left as it is, with nothing on standard error, as measured.

*Amended by BL-733 (2026-09-29):* [ADR-0226](ADR-0226-alt-svc-uses-h2-and-h3-alternatives-as-curl-se-s-build-looks-them-up.md)
widens decision 2 to `h2` and `h3` alternatives chosen by the version option, and replaces decision 4:
each redirect hop now looks its alternative up afresh.

## Consequences

The `* Connection #0 to host <host>:<port> left intact` line still names the origin where curl
names the alternative; the HTTP handler writes it, and a follow-up task fixes it there.
