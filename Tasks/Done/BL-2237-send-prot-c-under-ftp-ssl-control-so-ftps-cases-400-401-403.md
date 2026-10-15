---
id: BL-2237
title: Send PROT C under --ftp-ssl-control so FTPS cases 400, 401, 403, 406, 407, 408 and 1112 pass
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Console, Curl.Console.UnitTests, Curl.Cli.UnitLibrary/CommandLineOptions.cs, Curl.Protocol.Abstractions.UnitLibrary/ITransferContext.cs, Curl.Conformance.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-14
completed: 2026-10-14
---
# BL-2237 — Send PROT C under --ftp-ssl-control so FTPS cases 400, 401, 403, 406, 407, 408 and 1112 pass

## Goal

Under `--ftp-ssl-control` (TLS on the control connection only) Curl sends `PBSZ 0` then `PROT C`, as curl 8.22.0 does, where it now sends `PROT P`, so the FTPS cases 400, 401, 403, 406, 407, 408 and 1112 pass or fail only on another difference.

## Context

Found by BL-2221: test407 now runs and fails `<verify><protocol> differs at byte 51 (line 4): expected "PROT C\r\n", got "PROT P\r\n"`. `Curl.Conformance.UnitLibrary/CLAUDE.md` names the same difference for 400, 401, 403, 406, 408 and 1112, but no task covered it. curl's `lib/ftp.c` sends `PROT C` when `CURLOPT_USE_SSL` is `CURLUSESSL_CONTROL`. Start in `Curl.Protocol.Ftp.UnitLibrary` (`FtpDiagnosticLog` already logs `PROT C`). If the option's mapping lives in another library, add it to `touches` per the lane rules.

## Acceptance criteria

- [x] A unit test in `Curl.Protocol.Ftp.UnitTests` pins `PROT C` under control-only TLS and `PROT P` under `--ssl-reqd`.
- [x] Each of 400, 401, 403, 406, 407, 408 and 1112 that passes is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; each that still fails is named in Notes with its first difference.
- [x] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` shows no member outside a gate.

## Notes

- Cause: `FtpSession` already sent `PROT C` under `FtpTlsRequirement.ControlConnection`, but
  `CommandLineOptions.SslLevel` is `Required` under `--ftp-ssl-control` too (the mail protocols
  need that), so `FtpTlsRequirements.Of`, which checked `SslLevel == Required` first, always chose
  `AllConnections`. The FTP unit test set only `FtpSslControlOnly`, which hid it.
- Fix: `FtpTlsRequirements.Of` checks `FtpSslControlOnly` first; `TransferContextFactory` sets it
  only when `--ssl-reqd` is absent (`CommandLineOptions.SslRequired` made public to read), which
  keeps ADR-0102's measured ranking `--ssl-reqd` > `--ftp-ssl-control` > `--ssl`. No new ADR: the
  behaviour is ADR-0102's; only where the ranking is resolved moved. `ITransferContext` docs say so.
- Touches widened (no other Doing task named them): `Curl.Console` and `.UnitTests` (the mapping
  and its test), `Curl.Cli.UnitLibrary/CommandLineOptions.cs` (getter visibility),
  `Curl.Protocol.Abstractions.UnitLibrary/ITransferContext.cs` (doc comments only) and
  `Curl.Conformance.UnitLibrary/CLAUDE.md` (said these cases still differ).
- Tests: `ExecuteAsync_FtpSslControl_SendsProtCAndLeavesTheDataInPlaintext` now sets
  `SslLevel = Required` as the command line does; `ExecuteAsync_SslReqd_SendsProtP` pins `PROT P`;
  `Create_SslReqdBesideFtpSslControl_IsRequiredForEveryConnection` pins the ranking both orders.
- All seven cases (400, 401, 403, 406, 407, 408, 1112) pass through `CURL_UPSTREAM_CASE` and are on
  the passing list. Measure-CodeQuality for Ftp, Console and Cli: 0 failing members.

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
- 2026-10-14: Doing -> Done. Curl sends PROT C under --ftp-ssl-control (PROT P under --ssl-reqd); FTPS cases 400, 401, 403, 406, 407, 408 and 1112 pass
