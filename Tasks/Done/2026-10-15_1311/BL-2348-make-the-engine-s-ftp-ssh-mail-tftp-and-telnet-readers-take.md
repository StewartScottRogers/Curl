---
id: BL-2348
title: Make the engine's FTP, SSH, mail, TFTP and TELNET readers take EasyProtocolOptions instead of CommandLineOptions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2309]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Engine.IntegrationTests, Curl.Api.UnitLibrary/Options/Protocols, Curl.Api.UnitTests/Options/Protocols]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2348 — Make the engine's FTP, SSH, mail, TFTP and TELNET readers take EasyProtocolOptions instead of CommandLineOptions

## Goal

The engine reads its FTP, SSH, mail, TFTP and TELNET settings from the `EasyProtocolOptions` that `EasyOptionsFromCommandLine.ProtocolOf` builds (BL-2309), instead of `CommandLineOptions`; behaviour is byte for byte unchanged.

## Context

ADR-0506; plan phase 1 step 4. Split out of BL-2309 before any code, as BL-2347 was split out of BL-2308. The rules for every retarget task are in BL-2303's Context; BL-2309's Notes and ADR-0529 record the mapping.

Engine readers as of 2026-10-15: `EngineComposition.CreateFtpProtocolHandler` and `RoutingFtpProtocolHandler` (`-P`, `-Q`, `--ftp-*`, `--disable-epsv`/`--disable-eprt`, `-a`), `MailRequestOptionsMapping` (`--mail-from`, `--mail-rcpt`, `--mail-rcpt-allowfails`, `--mail-auth`), `SshOptionsMapping`, `SshKnownHostsFileSearch` and the SSH step of `TransferPerformer` (`--key`, `--pubkey`, `--hostpubmd5`, `--hostpubsha256`, `--knownhosts`, `--compressed-ssh`), and `TransferContextFactory`'s TFTP and TELNET fields (`--tftp-blksize`, `--tftp-no-options`, `-t`).

The known-hosts default search (`~/.ssh/known_hosts` when `--knownhosts` is not given) stays the tool's, per transfer, as ADR-0529 records; `-l`, `-B` and `--crlf` are the transfer group's (BL-2347). `CommandLineOptions` disappears from `Curl.Engine.UnitLibrary` only once BL-2343 to BL-2347 and this task are all Done. Tests that built `CommandLineOptions` only to drive these classes now build the records; tests through the whole command line stay as they are.

## Acceptance criteria

- [x] `EngineComposition`'s FTP handler factory, `RoutingFtpProtocolHandler`, `MailRequestOptionsMapping`'s sender and recipients, `SshOptionsMapping`, `SshKnownHostsFileSearch`, `TransferPerformer`'s SSH step and `TransferContextFactory`'s TFTP and TELNET fields take `EasyProtocolOptions`, not `CommandLineOptions`, for these settings.
- [x] `dotnet build` is clean (warnings are errors).
- [x] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Console,Curl.Engine.UnitLibrary` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes.
- [x] `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.

## Notes

- Plan: `TransferContextFactory.Create` gains an `EasyProtocolOptions` parameter after `EasyTransferOptions` for the FTP, TFTP and TELNET fields, `-a` and `-Q`; `MailRequestOptionsMapping.FromCommandLine` takes `EasySmtpOptions`; `SshOptionsMapping.FromCommandLine` became `Of(EasySshOptions, passphrase, knownHostsPath)`; `TransferPerformer.ResolveSshOptionsAsync` takes `EasySshOptions` for `--knownhosts` and the fingerprints. The runner passes `EasyOptionsFromCommandLine.ProtocolOf(options)`.
- `EngineComposition.CreateFtpProtocolHandler`, `RoutingFtpProtocolHandler` and `SshKnownHostsFileSearch` already read no `CommandLineOptions` setting, so they needed no change; criterion 1 holds for them as they are.
- Decisions (ADR-0535): `-Q` is rejoined by prefix (`+` pre, `-` post), which the FTP and SFTP handlers both sort by; a TFTP block size of 512 maps to `null` (not given), so the two existing tests that pin `null` stay as they are and the bytes are unchanged; `--pass`, `-k`, `-s` and `-S` stay on `CommandLineOptions`.
- Measured: build clean; fast tests all green (Curl.Engine.UnitTests 687 passed, Curl.Console.UnitTests 2826, Curl.Conformance.UnitTests 3129); `Measure-CodeQuality.ps1 -Library Curl.Console,Curl.Engine.UnitLibrary`: 100% line and branch, 0 failing members, worst CRAP 10. `PassingUpstreamCases.txt`, `CurlEasyErrorText.cs` and `InProcessCurl.RunAsync` are untouched.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. The engine reads FTP, SSH, mail, TFTP and TELNET settings from EasyProtocolOptions; behaviour unchanged
