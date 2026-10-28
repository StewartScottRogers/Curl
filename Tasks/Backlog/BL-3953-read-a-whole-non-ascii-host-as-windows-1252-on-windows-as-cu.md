---
id: BL-3953
title: Read a whole non-ASCII host as Windows-1252 on Windows, as curl 8.22.0 does (CGR-059)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs, Curl.Protocol.Abstractions.UnitTests/Cgr059Windows1252HostTests.cs]
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

## Log

- 2026-10-28: Created.
