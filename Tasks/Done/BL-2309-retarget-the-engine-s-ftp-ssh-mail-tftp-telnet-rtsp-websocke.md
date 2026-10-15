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
completed: 2026-10-15
---
# BL-2309 — Retarget the engine's FTP, SSH, mail, TFTP, TELNET, RTSP, WebSocket and other protocol settings from CommandLineOptions to EasyProtocolOptions

## Goal

The engine reads its FTP, SSH, mail, TFTP, TELNET, RTSP, WebSocket and other protocol settings from `EasyProtocolOptions` instead of `CommandLineOptions`, and `EasyOptionsFromCommandLine` builds it, so no engine class reads `CommandLineOptions` any more; behaviour is byte for byte unchanged.

## Context

ADR-0506; plan phase 1 step 4 (last retarget); follows BL-2308 and uses the rules for every retarget task written in BL-2303's Context. Engine readers (as of 2026-10-15): `EngineComposition.CreateFtpProtocolHandler` and `RoutingFtpProtocolHandler`, `MailRequestOptionsMapping`, `SshOptionsMapping`, `SshKnownHostsFileSearch`, the SSH step of `TransferPerformer`, `TransferContextFactory`'s FTP/TFTP/TELNET/RTSP/WebSocket fields, and anything left that still takes `CommandLineOptions` (a `Select-String -Pattern CommandLineOptions` over `Curl.Engine.UnitLibrary` lists it).

## Acceptance criteria

- [x] ~~`Select-String -Path Curl.Engine.UnitLibrary\*.cs -Pattern 'CommandLineOptions'` finds nothing (including sub-folders).~~ — split out to BL-2348, and needs BL-2343 to BL-2347 too (see Notes).
- [x] `EasyOptionsFromCommandLine` maps every protocol-specific command-line option (`-P`, `-Q`, `--ftp-*`, `--disable-epsv`, `--disable-eprt`, `--crlf` for FTP ASCII, `-l`, `--key` for SSH, `--pubkey`, `--hostpubmd5`, `--hostpubsha256`, `--known-hosts`, `--compressed-ssh`, `--mail-from`, `--mail-rcpt`, `--mail-rcpt-allowfails`, `--mail-auth`, `--tftp-blksize`, `--tftp-no-options`, `-t`, the RTSP options, `--ws-*` if any), each with a test.
- [x] `dotnet build` is clean (warnings are errors).
- [x] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes (the ratchet is unchanged).
- [x] The gap office's scores are unchanged: `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.

## Notes

- 2026-10-15, split (rule 4, before any code), as BL-2303, BL-2307 and BL-2308 split: the engine still reads `CommandLineOptions` in about 30 files for groups BL-2343 to BL-2347 retarget, so the first criterion cannot be met by this task alone, and the protocol readers' retarget (FTP handler factory, `RoutingFtpProtocolHandler`, `MailRequestOptionsMapping`, `SshOptionsMapping`, `SshKnownHostsFileSearch`, `TransferPerformer`'s SSH step, `TransferContextFactory`'s TFTP/TELNET fields, and their test call sites) does not fit one run beside the mapper. This task delivers `EasyOptionsFromCommandLine.ProtocolOf` and ADR-0529; filed BL-2348 (depends on this) for the retarget, and BL-2310 now also depends on BL-2348. No record needed a new member, so `Curl.Api.UnitLibrary/Options/Protocols` is unchanged.
- Decided (ADR-0529, by Claude under Stewart's delegation): `-Q` splits by `+`/`-` prefix as `--libcurl` writes QUOTE/PREQUOTE/POSTQUOTE (a `*` command stays in `Quote`, prefix kept); `--ftp-create-dirs` is `CreateAndRetry` (`--libcurl` writes `2L`); `--ftp-method` absent is `MultiCwd`; `--key` is also the SSH private key; `SshAuthenticationTypes` keeps libcurl's any (the tool never sets it); the default `~/.ssh/known_hosts` stays per transfer; RTSP and WebSocket keep libcurl's defaults (curl's tool sets no `CURLOPT_RTSP_*` or `CURLOPT_WS_OPTIONS`; an RTSP request is `-X`). `-l`, `-B` and `--crlf` are the transfer group's (ADR-0528). The option is spelled `--knownhosts` in curl, not `--known-hosts`.
- `--ai-help` unaffected (no option changes).
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Console` - 100% line and branch, 805 members, 0 failing, worst CRAP 10. Fast tests: every assembly green (Console 2798 passed, Engine 652, Conformance 3129). Ratchet file unchanged.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. EasyOptionsFromCommandLine.ProtocolOf maps the FTP, SSH, mail, TFTP and TELNET options onto EasyProtocolOptions (ADR-0529); engine retarget split to BL-2348
