---
id: BL-240
title: Wire scheme guessing, URL globbing and the IPFS rewrite in Curl.Console
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-204, BL-207, BL-210, BL-230, BL-353]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-240 — Wire scheme guessing, URL globbing and the IPFS rewrite in Curl.Console

## Goal

URLs pass through BL-204, BL-207 and BL-210 before dispatch in `Curl.Console`.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item W11. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- BL-230 added as a dependency beyond the plan so the split runner is in place.

## Acceptance criteria

- [x] `curl example.com`, `curl 'http://h/[1-3]' -o '#1.txt'` and `curl ipfs://<cid>` behave as measured on curl 8.21.0, over fakes.
- [x] `dotnet build Curl.Console -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Plan item: W11 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- BL-353 added as a dependency by BL-210 (2026-09-27): the IPFS rewrite needs the `--ipfs-gateway` value, which Curl.Cli does not parse yet.
- Delivered directly in the session rather than through the full `/feature` agent chain: one project, wiring three finished `Curl.Core` pieces, driven by measurement. `CurlCommandRunner.TransferAllAsync` now parses each command-line URL with `UrlGlob.TryParse` (`UrlGlob.Unglobbed` under `-g`) and runs one `UrlTransfer` (new, `Curl.Console/UrlTransfer.cs`) per match. `TransferUrlAsync` rewrites an IPFS URL with `IpfsGatewayRewriter`, then adds the guessed scheme with `UrlSchemeGuesser.AddGuessedScheme`, then resolves a `-T` URL. The runner takes a new optional `readEnvironmentVariable` (none set when not given); `CurlComposition.CreateRunner` passes the process environment. The gateway file is read through the runner's `IDataFileReader`.
- Measured with `/mingw64/bin/curl` 8.21.0 (Schannel) on 2026-09-27. Unreachable loopback ports and `file://` URLs meant no server was needed:
  - `curl -s -m 1 -w '%{url}|%{url_effective}|%{urlnum}|%{filename_effective}|%{exitcode}\n' -o '#1.txt' 'http://127.0.0.1:1/[1-3]'` printed `http://127.0.0.1:1/1|http://127.0.0.1:1/1|0|1.txt|28` for 1, 2 and 3.
  - `'localhost:1/{a,b}' 'http://127.0.0.1:1/x'` printed `localhost:1/a|http://localhost:1/a|0|28`, then `.../b` with urlnum 0, then `.../x` with urlnum 1. `%{url}` stays as typed, `url_effective` has the guessed scheme, and urlnum counts command-line URLs. `ftp.localhost:1/x` printed `ftp.localhost:1/x|ftp://ftp.localhost:1/x|ftp|28`.
  - `-o 'o#1' file:///.../{a,b}.txt file:///.../a.txt -o last` printed urlnum/xfer_id `0|0|oa`, `0|1|ob`, `1|2|last`, and wrote all three files. `-D h.txt` over `{a,b}` holds both header blocks: the first transfer truncates and later ones append.
  - `-sS 'http://127.0.0.1:1/[3-1]' 'http://127.0.0.1:1/y'` printed `curl: (3) bad range in position 25:`, the URL, then 24 spaces and `^`. Exit 3, and the second URL was not transferred. After a good URL the good one runs first and the exit is still 3. Under `-s` nothing is printed. `{nope,a}` with the first failing (exit 37) goes on to `a`.
  - `-g -o 'q?x'` opens `q?x` as written (exit 23 on Windows); without `-g`, `-o 'q?y'` writes `q_y`.
  - `--ipfs-gateway http://127.0.0.1:1 'ipfs://bafy{a,b}/x'` printed `%{url}` and `url_effective` as `http://127.0.0.1:1/ipfs/bafya/x`, then `bafyb`, both urlnum 0. `--ipfs-gateway 127.0.0.1:1 ipfs://bafyabc` gave `http://127.0.0.1:1/ipfs/bafyabc`.
  - With no gateway (`HOME` without `.ipfs`), even under `-s`: `curl: IPFS automatic gateway detection failed`, then the try-help line, and exit 37. The next URL is not transferred. `-w` printed `errormsg` `Could not read a file:// file`, `url` `ipfs://bafyabc`, `url_effective` empty, urlnum 0, xfer_id -1, conn_id -1, scheme empty. `filename_effective` was the `-o` name (`--output-dir sub -o 'x#1?.out'` gave `sub/xa_.out`). The `-w` line feed was LF without `-o` and CR LF with one.
  - `--ipfs-gateway 'http://h:1/?q'` gave `curl: malformed target URL`, the try-help line, exit 3, and `errormsg` `URL using bad/illegal format or missing URL`.
- Defaults taken (rule 1): the gateway file is decoded as UTF-8 (the rewriter works on text; curl reads bytes and only the first line matters). The IPFS check parses the URL without `--path-as-is`, as curl's rewrite parses it with its own flags. An IPFS failure does not use up an `%{xfer_id}`, which does not matter because it ends the run. No ADR: every behaviour pinned is curl's measured one.
- Not modelled, filed: `%{url_effective}` for a URL with no path lacks curl's `/` (`localhost:1` gives `http://localhost:1`, curl `http://localhost:1/`), BL-371. `-O` on an IPFS URL, which curl refuses with exit 1 before any gateway lookup, BL-372. `Curl.Console`'s `WindowsOutputFileNameSanitizer.Sanitize` now duplicates Core's for remote names only, BL-370. `-T` globs are the existing BL-366.
- Tests: `CurlCommandRunnerUrlExpansionTests` (21), `UrlTransferTests` (4), and `CurlCompositionTests.CreateRunner_IpfsUrl_ReadsTheGatewayFromTheProcessEnvironment`. That last test sets `IPFS_GATEWAY` in-process to a gateway curl refuses, so it exits 3 with no connection, and restores the variable afterwards. Existing tests unchanged.
- Gates: `dotnet build` clean (0 warnings). Fast run green: Curl.Console.UnitTests 629 passed, and every other test project 0 failed. `dotnet format --verify-no-changes` is clean. `Measure-CodeQuality.ps1 -Library Curl.Console -IncludeIntegration` gives 100% line, 100% branch, 0 failing members and worst CRAP 10. Without `-IncludeIntegration` it flags only `DiskWriteOutFileOpener.TryOpen`, whose tests are Integration (as in BL-280/BL-328).

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Backlog. Lane handed over mid-run while the factory's restart logic was fixed; the run had only just resumed and left no work.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. Curl.Console expands URL globs (#N in -o, -g), rewrites ipfs/ipns URLs to their gateway and guesses a missing scheme before dispatch, as measured on curl 8.21.0
