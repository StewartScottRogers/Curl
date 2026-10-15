# ADR-0533 — The engine's cookie, alt-svc and HSTS steps read `EasyHttpOptions`

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2346.

## Context

ADR-0527 built `EasyOptionsFromCommandLine.HttpOf` and ADR-0532 had the HTTP request mappings read it.
BL-2346 does the same for `CookieEngine`, `AltSvcTransferCache`, `HstsCacheFile` and `TransferPerformer`'s
cookie, HSTS and alt-svc steps, with behaviour byte for byte unchanged. `HttpOf` joined the `-b name=value`
strings with `"; "` every time, while `CookieEngine` joined them as curl 8.21.0's `cookie_setopts` does,
leaving the space out before a string that starts with a space or a tab.

## Decision

- **`HttpOf` joins the `-b` strings as `cookie_setopts` does.** curl's tool builds the joined string once and
  hands that same string to `CURLOPT_COOKIE`, so `--libcurl` writes it too; the engine's bytes (measured,
  BL-1391) are the right ones, and `HttpOf`'s plain `"; "` join was the difference. `-b a=1 -b " b=2"` now
  gives `Cookie` `a=1; b=2` and `-b a=1 -b "<tab>b=2"` gives `a=1;<tab>b=2`. A `-b` string always holds a
  `=`, so it is never empty and the separator always follows a non-empty start.
- **`CookieEngine.FromHttpOptions` replaces `FromCommandLine`.** It reads `Cookie` (the joined strings, sent
  as they are and measured against the 8200-byte limit), `CookieFiles`, `CookieJar`, `CookieSession` (`-j`)
  and `HttpHeaders` (an `-H` naming `Cookie` still drops the strings; `--json`'s two lines never name it).
  The engine is created when `Cookie` is set, a cookie file is named or a jar is given - the same groups
  `-b` or `-c` created it for, since `-b ""` is a file.
- **`AltSvcTransferCache.OpenAsync` takes the record** for `AltSvc` (empty or `null` reads and writes
  nothing) and `HttpVersion`, which its lookup table now keys on `Curl.Api`'s `HttpVersion`:
  `None` for no version option, `Http10`, `Http2PriorKnowledge` and `Http2Tls` looking nothing up.
  `HttpVersionMapping.FromRequested` no longer serves it. `--connect-to` stays on `CommandLineOptions`
  until BL-2343.
- **`HstsCacheFile.ReadAsync` and `WriteAsync` take the record** and do nothing when `Hsts` is `null` or
  empty, so the performer no longer checks `--hsts` itself. `HstsControl` is not read: `HttpOf` sets it
  exactly when `Hsts` is set.
- **`TransferPerformer`'s `SwitchToHttpsForHstsAsync`, `OpenAltSvcCacheAsync` and
  `WriteCookieJarAltSvcAndHstsFilesAsync` take the record beside `CommandLineOptions`**, which they still
  read for the `-G` / `--url-query` query, `--path-as-is` and `--connect-to`. `LoadCookieFilesAsync` and
  `SetupFailureAsync` read no cookie setting from `CommandLineOptions` - their cookie settings come through
  `CookieEngine` - so their signatures are unchanged.

## Consequences

The runner builds the record with `HttpOf` for each transfer's setup and cleanup, and
`CurlComposition.SharingRunCookies` builds it once per option group. Tests of `CookieEngine` build the
record directly; tests through the whole command line stay.
