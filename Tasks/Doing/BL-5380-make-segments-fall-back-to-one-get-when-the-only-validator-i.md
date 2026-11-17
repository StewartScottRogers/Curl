---
id: BL-5380
title: Make --segments fall back to one GET when the only validator is a weak ETag or Last-Modified
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.SegmentedDownload.UnitLibrary, Curl.Extension.SegmentedDownload.UnitTests, Documentation/Planning/Decisions/ADR-1087-segments-downloads-a-plain-http-get-as-parallel-byte-ranges-in-a-transfer-hook.md]
requirement: none
created: 2026-11-17
completed:
---
# BL-5380 — Make --segments fall back to one GET when the only validator is a weak ETag or Last-Modified

## Goal

`--segments` joins byte ranges only when the representation has a strong `ETag`; with a weak ETag or only a `Last-Modified` it does one plain GET, so the output is never a mix of two representations.

## Context

From the outside fitness review of 2026-11-17 (commit 9561bbe, `preview` build), finding EXT-074 (Medium, Confirmed: Windows 2 runs, Linux 1 run; silent corrupt download, exit 0). Area: `--segments`, Windows and Linux. Conflicts with ADR-1087's 2026-11-16 note, which deliberately allows joining "when every segment repeats the same validators"; this task updates that ADR.

**Repro.** A server whose `/rgweak` serves 300,000 bytes that differ on each request (`data[i] = (i*7 + n) % 251`, `n` = request count), always with `ETag: W/"same"` and `Accept-Ranges: bytes`, honouring `Range` with a correct 206 and `Content-Range`.
- `curl -sS -m 15 --segments 4 http://127.0.0.1:43001/rgweak -o w.out -w "%{http_code} %{size_download}\n"`
- Check each 75,000-byte piece's version `n = (byte[k] - 7k) mod 251` at k = 0, 75000, 150000, 225000.

**Expected.** RFC 9110 section 15.3.7.3 allows combining partial responses only when they share a strong validator; ADR-1087's goal is "the output is never a mix of two representations". Treat a weak ETag or a Last-Modified-only response like no validator: one plain GET.
**Actual (PORT).** `206 300000`, exit 0; five requests (probe plus four ranges, no `If-Range`); `w.out` holds versions 4, 5, 6, 7. The same with a fixed `Last-Modified` and no ETag.
**Where to start.** `Curl.Extension.SegmentedDownload.UnitLibrary/SegmentedDownloadTransferHook.cs` (line ~73 uses `If-Range` only for a strong tag; the join check at lines ~85-86 accepts any matching ETag and Last-Modified) and `CapturedResponse.cs` line ~30 (`HasValidator` is true for any ETag or Last-Modified; it should require a strong ETag).

## Acceptance criteria

- [ ] New tests in `Curl.Extension.SegmentedDownload.UnitTests` (a new file) prove a probe with a weak ETag (`W/"same"`) makes the hook run one plain GET and write that one response.
- [ ] A test proves a probe with only `Last-Modified` (no ETag) does the same.
- [ ] A test proves a probe with a strong `ETag: "s"` still sends the segment ranges, each with `If-Range: "s"`.
- [ ] ADR-1087 is updated so its 2026-11-16 note no longer allows joining on weak or Last-Modified-only validators.
- [ ] Tests are platform-neutral.
- [ ] Hand check recorded in the Log: the command against `/rgweak` writes one consistent version (probe plus one plain GET); with a fixed strong `ETag: "s"` it still sends 4 ranges, each with `If-Range: "s"`.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: the command writes one consistent version (probe plus one plain GET); with a fixed strong `ETag: "s"` it still sends 4 ranges, each with `If-Range: "s"`.
- ADR-1087 is `Documentation/Planning/Decisions/ADR-1087-segments-downloads-a-plain-http-get-as-parallel-byte-ranges-in-a-transfer-hook.md`; mark the update "Decided by Claude under Stewart's delegation".

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: 6d87d298 56974e2e 76cff19b bc7d58ca c1cf21f9 396ae644 a0b49a29.
