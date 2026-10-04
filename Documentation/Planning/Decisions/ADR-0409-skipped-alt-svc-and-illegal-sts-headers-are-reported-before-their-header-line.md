# ADR-0409 — Skipped Alt-Svc alternatives and illegal STS headers are reported before their header line

- **Status:** Accepted
- **Date:** 2026-10-03
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

curl 8.21.0 writes a `-v` info line while it reads an `Alt-Svc` or `Strict-Transport-Security`
response header its caches will not take, so the line sits just before that header's `< ` line.
Measured 2026-10-03 with curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1 -Tls`,
`-sv -k --resolve h.test:PORT:127.0.0.1 --hsts <file> --alt-svc <file> https://h.test:PORT/a`,
reply headers `Strict-Transport-Security: max-age=abc` then `Alt-Svc: <value>` (BL-1407):

| Header | Line written just before the header line |
|---|---|
| `Strict-Transport-Security: max-age=abc` | `* Illegal STS header skipped` |
| the same, host an IP address | none |
| `Alt-Svc: h2=":abc"` | `* Unknown alt-svc port number, ignoring.` |
| `Alt-Svc: h2="[::1]:99999"` | `* Unknown alt-svc port number, ignoring.` |
| `Alt-Svc: h2="host:"` | `* Unknown alt-svc port number, ignoring.` |
| `Alt-Svc: h2="[::1:443"` | `* Bad alt-svc IPv6 hostname, ignoring.` |
| any of these over plain `http://` | none |

curl's source (tag `curl-8_21_0`) has the texts in `lib/http.c` lines 3550-3573
(`infof(data, "Illegal STS header skipped")` when `Curl_hsts_parse` fails) and `lib/altsvc.c`
lines 515-545 (`Bad alt-svc hostname, ignoring.` for a host over the length limit,
`Bad alt-svc IPv6 hostname, ignoring.`, `Unknown alt-svc port number, ignoring.`).

Curl writes none of these today. `IAltSvcStore.StoreFromResponse` returns only the alternatives
added, which `HttpProtocolHandler.StoreAltSvc` reports as `Added alt-svc:` lines before the header
line; `AltSvcHeaderParser` stops at a bad host or port without saying why. HSTS headers are learned
only after the transfer, by `HstsTransferPolicy.LearnFrom` reading the report, so no line can be
written in its place during header reading.

## Decision

1. **`Curl.Protocol.Abstractions.UnitLibrary` (contract, smallest change).**
   - `IAltSvcStore.StoreFromResponse` returns `IReadOnlyList<AltSvcHeaderOutcome>` instead of
     `IReadOnlyList<AltSvcAlternative>`: one entry per alternative read, in header order, each either
     the `AltSvcAlternative` added or an `AltSvcSkipReason` (`BadHostname`, `BadIpv6Hostname`,
     `UnknownPortNumber`). The store reports facts; the handler owns the `-v` text, as it does for
     `Added alt-svc:` today.
   - A new `IHstsStore` with `bool StoreFromResponse(CurlUrl origin, string headerValue, DateTimeOffset now)`,
     `false` only when the header is illegal and curl would write `Illegal STS header skipped`
     (an IP-address host returns `true` and stores nothing, as curl writes no line for it), and a
     new `HttpRequestOptions.HstsStore` beside `AltSvcStore`.
2. **`Curl.Core.UnitLibrary`.** `AltSvcHeaderParser` records why it stopped (host over
   `AltSvcEntry.MaxHostLength`, unclosed or overlong IPv6 literal, empty, non-numeric or
   out-of-range port) and `AltSvcCache` returns it in the outcome list. `HstsTransferPolicy`
   implements `IHstsStore`, applying one header at a time; `LearnFrom` is removed once the console
   uses the per-header seam.
3. **`Curl.Protocol.Http.UnitLibrary`.** `StoreAltSvc` writes `Added alt-svc: ...` or the skip
   reason's text for each outcome, in order, before the header line. A new per-header HSTS step
   calls `options.HstsStore` for each `Strict-Transport-Security` header of an `https` response and
   writes `Illegal STS header skipped` on `false`, before the header line.
4. **`Curl.Console`.** `AltSvcTransferCache` returns the new outcome list; the runner sets
   `HstsStore` to its `HstsTransferPolicy` when `--hsts` is given and stops calling `LearnFrom`
   after the transfer.

## Why not write the lines after the transfer

The lines' place is the point: curl writes them mid-header, before the `< ` line, and the `-v`
stream after the transfer already holds the rest of the headers, the body and the connection lines.
Writing them afterwards would put them in the wrong place in every case, and a drop-in replacement
cannot do that. Learning HSTS per header also matches curl, which applies each header as it is read.

## Alternatives considered

- **Return the `-v` text from the store.** Lost: puts curl's verbose wording in `Curl.Core` and the
  cache, two places owning the same lines; the handler already owns `Added alt-svc:`.
- **An `ITransferEvents` passed into the store.** Lost: a wider contract change in Abstractions for
  the same result, and the store would then need the transfer context.
- **Keep `LearnFrom` and add a separate validity check per header.** Lost: parses every header twice
  and leaves two ways to learn HSTS.

## Implementation tasks

Filed in dependency order: the Abstractions contract, then Core, then the HTTP handler and
Curl.Console wiring (BL-1418, BL-1419, BL-1420).
