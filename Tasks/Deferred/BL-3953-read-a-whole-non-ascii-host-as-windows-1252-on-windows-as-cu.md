---
id: BL-3953
title: Read a whole non-ASCII host as Windows-1252 on Windows, as curl 8.22.0 does (CGR-059)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs, Curl.Protocol.Abstractions.UnitTests/Cgr059Windows1252HostTests.cs, Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHostNameMapping.cs, Curl.Protocol.Abstractions.UnitTests/Cgr059PercentEncodedHostTests.cs, Curl.Protocol.Abstractions.UnitTests/Cgr059BidiTests.cs, Curl.Protocol.Abstractions.UnitTests/Cgr059LabelRulesTests.cs, Curl.Protocol.Abstractions.UnitTests/Cgr019Tests.cs, Curl.Protocol.Abstractions.UnitTests/CurlUrlTests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-3953 — Read a whole non-ASCII host as Windows-1252 on Windows, as curl 8.22.0 does (CGR-059)

## Goal

On Windows, Curl converts a non-ASCII host name from bytes read as Windows-1252, every byte and not only those that are not UTF-8, as curl 8.22.0 (Schannel, libidn2 2.3.8) does.

## Context

Found by BL-3886 on 2026-10-28. `CurlUrlHost.cs` (`StrayBytesAsWindows1252`, `ReadStrayBytesAsWindows1252`, ADR-0939 from BL-3884) reads only bytes that are not valid UTF-8 as Windows-1252. Real curl 8.22.0 on Windows (`/ucrt64/bin/curl`) reads the whole host in the ANSI code page: libidn2's `idn2_lookup_ul` converts from the locale charset. Measured with Record-CurlExchange.ps1, the URL in a UTF-8 config file (`-K`, `url = "..."`) and `--connect-to ::127.0.0.1:20200`:

- `http://%C3%BC.example:20200/a`: real curl exit 0, `Host: xn--14-ria7423a.example:20200` (`Ã¼`, with `¼` mapped to `1⁄4`). Curl: `Host: xn--tda.example:20200`.
- `http://a%D7%90.example:20200/a`: real curl exit 3, `curl: (3) URL using bad/illegal format or missing URL` (0x90 is undefined in Windows-1252). Curl: exit 0, `Host: xn--a-0hc.example:20200`.

Linux (UTF-8 locale) is right today and must stay so: `Cgr059BidiTests` and `Cgr059PercentEncodedHostTests` pin it with `strayBytesAsWindows1252: false`. Check how a host reaches `CurlUrlHost` from the Windows command line (Curl already turns `aא` typed in an argument into `a?` as curl does) and whether a host typed unescaped in a config file gives the same bytes, before deciding where the conversion happens. Amend ADR-0939 or add an ADR.

## Acceptance criteria

- [ ] On Windows, the two URLs above, run through real curl and Curl as described, give the same exit code, stdout bytes, stderr text and request bytes; the commands and results are recorded in Notes.
- [ ] A unit test in `Cgr059Windows1252HostTests.cs` pins the Windows-1252 reading of valid UTF-8 bytes (`%C3%BC` to `xn--14-ria7423a`, `a%D7%90` refused) and fails before the fix.
- [ ] The UTF-8 locale tests (`Cgr059BidiTests`, `Cgr059PercentEncodedHostTests`, `Cgr059LabelRulesTests`) stay green.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- 2026-10-28 (lane 4, first claim; code left uncommitted for the shift's stash):
  - Touches widened. No Doing task on origin/work/dark-factory names these files. `CurlUrlHostNameMapping.cs` is needed because `¼` must map to `1⁄4`. `Cgr059PercentEncodedHostTests.cs` is needed because its old `%F0%90%82%80a` test was wrong: real curl refuses that host. The other Cgr059 tests need the rename `TryNormalizeReadingStrayBytes` to `TryNormalizeReadingHost(hostAsWindows1252:)`. `Cgr019Tests.cs` and `CurlUrlTests.cs` hold tests that now fail on Windows (below).
  - Measured real curl with Record-CurlExchange.ps1, `-sS -K <utf-8 cfg with url = "...">` and `--connect-to ::127.0.0.1:20200`:
    - `%C3%BC.example`: exit 0, `xn--14-ria7423a`.
    - `a%D7%90.example`: exit 3, `URL using bad/illegal format or missing URL`.
    - `%F0%90%82%80a.example`: exit 3, same text.
    - `ü.example` typed unescaped in the config file: exit 0, `xn--14-ria7423a`.
    - `ü` typed in an argument: exit 0, `xn--tda`.
  - Stashed change: on Windows, `ReadHostAsWindows1252` turns each typed character into its Windows-1252 byte (its UTF-8 when Windows-1252 lacks it) and each escape into its raw byte, then reads every byte back as Windows-1252. `Latin1Mappings` was added to `CurlUrlHostNameMapping`. Run through Curl the same way, the first two criterion URLs, `%F0...` and the argument case all match real curl in exit code, stdout, stderr and request bytes. The config-file literal does not; that is filed as BL-3956.
  - Still failing (why this run requeued, budget spent):
    - 6 rows of `Cgr019Tests.TryNormalize_HostThatIsNotAscii_ConvertsTheMappedName`: ΣΣ, İstanbul, Ａb, a‍b, an NFD école, ﬁle.
    - `CurlUrlTests.TryParse_ForEachScheme_ExposesTheMembersHandlersRead` (`ex%C3%A5mple`).
    - Upstream test1448 (redirect to UTF-8 IDN `åäö.se`): the redirect is no longer followed.
    - The unit tests use the platform default, so they now read Windows-1252 on Windows. Next step: pin them to `hostAsWindows1252: false` (UTF-8 locale) or give them per-platform answers. Then find how a `Location` host reaches `CurlUrlHost`: real curl sees its raw UTF-8 bytes, so measure real Windows curl on test1448 before deciding, and keep or hold the case on the ratchet per platform. Then write the ADR-0939 amendment and run Measure-CodeQuality for Curl.Protocol.Abstractions.UnitLibrary.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 37a11300 0ffe192b a91492c4 779790c0.
- 2026-10-28: Doing -> Backlog. Budget spent mid-fix: Windows-1252 host reading works for the measured URLs, but Cgr019Tests, CurlUrlTests and upstream test1448 need per-source handling; next steps in Notes, code in the stash
- 2026-10-28: Backlog -> Deferred. Split into BL-3957, BL-3958 and BL-3959 after hitting the cost cap
