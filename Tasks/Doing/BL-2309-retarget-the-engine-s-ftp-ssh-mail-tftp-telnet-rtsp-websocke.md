---
id: BL-2309
title: Retarget the engine's FTP, SSH, mail, TFTP, TELNET, RTSP, WebSocket and other protocol settings from CommandLineOptions to EasyProtocolOptions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2308, BL-2300]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Api.UnitLibrary/Options/Protocols, Curl.Api.UnitTests/Options/Protocols]
requirement: none
created: 2026-10-15
completed:
---
# BL-2309 — Retarget the engine's FTP, SSH, mail, TFTP, TELNET, RTSP, WebSocket and other protocol settings from CommandLineOptions to EasyProtocolOptions

## Goal

The engine reads its FTP, SSH, mail, TFTP, TELNET, RTSP, WebSocket and other protocol settings from `EasyProtocolOptions` instead of `CommandLineOptions`, and `EasyOptionsFromCommandLine` builds it, so no engine class reads `CommandLineOptions` any more; behaviour is byte for byte unchanged.

## Context

ADR-0506; plan phase 1 step 4 (last retarget); follows BL-2308 and uses the rules for every retarget task written in BL-2303's Context. Engine readers (as of 2026-10-15): `EngineComposition.CreateFtpProtocolHandler` and `RoutingFtpProtocolHandler`, `MailRequestOptionsMapping`, `SshOptionsMapping`, `SshKnownHostsFileSearch`, the SSH step of `TransferPerformer`, `TransferContextFactory`'s FTP/TFTP/TELNET/RTSP/WebSocket fields, and anything left that still takes `CommandLineOptions` (a `Select-String -Pattern CommandLineOptions` over `Curl.Engine.UnitLibrary` lists it).

## Acceptance criteria

- [ ] `Select-String -Path Curl.Engine.UnitLibrary\*.cs -Pattern 'CommandLineOptions'` finds nothing (including sub-folders).
- [ ] `EasyOptionsFromCommandLine` maps every protocol-specific command-line option (`-P`, `-Q`, `--ftp-*`, `--disable-epsv`, `--disable-eprt`, `--crlf` for FTP ASCII, `-l`, `--key` for SSH, `--pubkey`, `--hostpubmd5`, `--hostpubsha256`, `--known-hosts`, `--compressed-ssh`, `--mail-from`, `--mail-rcpt`, `--mail-rcpt-allowfails`, `--mail-auth`, `--tftp-blksize`, `--tftp-no-options`, `-t`, the RTSP options, `--ws-*` if any), each with a test.
- [ ] `dotnet build` is clean (warnings are errors).
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes (the ratchet is unchanged).
- [ ] The gap office's scores are unchanged: `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
