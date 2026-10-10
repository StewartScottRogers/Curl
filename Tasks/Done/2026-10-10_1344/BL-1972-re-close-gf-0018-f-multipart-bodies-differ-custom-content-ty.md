---
id: BL-1972
title: Re-close GF-0018: -F multipart bodies differ: custom Content-Type, several files in one -F, quoted file names with , ; "
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-1972 — Re-close GF-0018: -F multipart bodies differ: custom Content-Type, several files in one -F, quoted file names with , ; "

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0018 (-F multipart bodies differ: custom Content-Type, several files in one -F, quoted file names with , ; "), so a later gap analysis measures each of `behaviour:test277`, `behaviour:test669`, `behaviour:test1133`, `behaviour:test1315` as `match`.

## Context

This is a Re-close task: every earlier task for GF-0018 ([BL-1811]) is Done, but the latest gap analysis still measures a gap, so the fix did not hold.

- Finding: GF-0018, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test277`, `behaviour:test669`, `behaviour:test1133`, `behaviour:test1315`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Every item expects 'upstream test<N> passes'. test277 (-F name=daniel -H 'Content-Type: text/info'): <verify><protocol> differs at byte 93 (line 5): expected 'Content-Length: 158', got 'Content-Type: text/info'. test669 (-H 'Content-type: multipart/form-data; charset=utf-8'): expected 'Content-Length: 260', got 'Content-type: multipart/form-data; charset=utf-8'. test1133 (quoted file name with ',', ';', '"'): request differs at byte 634 from the reference curl. test1315 (-F 'file=@a,b;type=magic/content,c'): request differs at byte 390 from the reference curl. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 277,669,1133,1315

Suggestion, copied from the finding:

In Curl.Cli.UnitLibrary's -F parser and the multipart writer it feeds: merge a user Content-Type into the generated one (keep the user's type, append '; boundary=...', placed after Content-Length, as curl's header order shows). Parse '@a,b,c' with per-file ';type=' as several file parts. Parse a quoted file name holding ',', ';' and '"' and escape it in Content-Disposition as curl 8.21.0 does.

## Acceptance criteria

- [x] `behaviour:test277`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [x] `behaviour:test669`: Curl answers what curl 8.21.0 answers, `upstream test669 passes`, so the item measures `match`.
- [x] `behaviour:test1133`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [x] `behaviour:test1315`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- Touches widened (lane 2, 2026-10-10) to `Curl.Core.UnitLibrary`/`.UnitTests` and
  `Curl.Console`/`.UnitTests`: the part disposition is chosen by Core's
  `MultipartFormBodyBuilder` and the `-H` value reaches it through Console's mapping. No task
  in Doing on `origin/work/dark-factory` named any of them.
- Measured 2026-10-10 with `Record-CurlExchange.ps1`, curl 8.21.0 (Schannel) against this
  branch, `-F name=daniel -H 'Content-Type: text/info'` (test277): curl sends
  `Content-Disposition: attachment; name="name"` and Content-Length 158; Curl sent `form-data`
  and 157. That one byte is why the item still failed after BL-1811 fixed the header order.
  libcurl's `Curl_mime_prepare_headers` gives the parts `form-data` only when the request type
  (first `-H` naming `Content-Type`, `:` or `;` form) matches `multipart/form-data`
  (any case, parameters allowed - test669 stays `form-data`); otherwise each named part falls
  back to `attachment`.
- Also measured: an empty `-H 'Content-Type:'` or `-H 'Content-Type;'` makes curl send
  `Content-Type: ; boundary=...` with `attachment` parts. BL-1811 had left the type out
  for an empty value; `HttpRequestHeadFormatter.FormContentTypeOf` now takes the first
  `Content-Type` value, empty included.
- Fix: `MultipartFormBodyBuilder.BuildAsync(parts, nameEscaping, requestContentType, ct)`;
  `MultipartFormPartMapping.RequestContentTypeOf` reads it from `-H` as `Curl_checkheaders`
  does. After the fix test277's request is byte-identical to curl's (boundary aside).
- test1133 and test1315: BL-1847 measured Curl's requests byte-identical to curl 8.21.0's;
  the gap comes from the gap office's cross-check normalising only the top-level boundary,
  not the nested `multipart/mixed` one. That fix is in `Gap/`, which a lane may not read or
  change; an interactive session must fix `Set-FixedMultipartBoundary` before GF-0018 can
  close. No option added or changed, so `--ai-help` is unaffected.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. -F with an -H Content-Type now sends curl's attachment/form-data parts and empty-type header (test277, test669); test1133/1315 wait on the gap office's nested-boundary normaliser
