---
id: BL-2237
title: Send PROT C under --ftp-ssl-control so FTPS cases 400, 401, 403, 406, 407, 408 and 1112 pass
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-14
completed:
---
# BL-2237 — Send PROT C under --ftp-ssl-control so FTPS cases 400, 401, 403, 406, 407, 408 and 1112 pass

## Goal

Under `--ftp-ssl-control` (TLS on the control connection only) Curl sends `PBSZ 0` then `PROT C`, as curl 8.22.0 does, where it now sends `PROT P`, so the FTPS cases 400, 401, 403, 406, 407, 408 and 1112 pass or fail only on another difference.

## Context

Found by BL-2221: test407 now runs and fails `<verify><protocol> differs at byte 51 (line 4): expected "PROT C\r\n", got "PROT P\r\n"`. `Curl.Conformance.UnitLibrary/CLAUDE.md` names the same difference for 400, 401, 403, 406, 408 and 1112, but no task covered it. curl's `lib/ftp.c` sends `PROT C` when `CURLOPT_USE_SSL` is `CURLUSESSL_CONTROL`. Start in `Curl.Protocol.Ftp.UnitLibrary` (`FtpDiagnosticLog` already logs `PROT C`). If the option's mapping lives in another library, add it to `touches` per the lane rules.

## Acceptance criteria

- [ ] A unit test in `Curl.Protocol.Ftp.UnitTests` pins `PROT C` under control-only TLS and `PROT P` under `--ssl-reqd`.
- [ ] Each of 400, 401, 403, 406, 407, 408 and 1112 that passes is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; each that still fails is named in Notes with its first difference.
- [ ] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` shows no member outside a gate.

## Notes

## Log

- 2026-10-14: Created.
