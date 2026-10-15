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
completed:
---
# BL-2348 — Make the engine's FTP, SSH, mail, TFTP and TELNET readers take EasyProtocolOptions instead of CommandLineOptions

## Goal

The engine reads its FTP, SSH, mail, TFTP and TELNET settings from the `EasyProtocolOptions` that `EasyOptionsFromCommandLine.ProtocolOf` builds (BL-2309), instead of `CommandLineOptions`; behaviour is byte for byte unchanged.

## Context

ADR-0506; plan phase 1 step 4. Split out of BL-2309 before any code, as BL-2347 was split out of BL-2308. The rules for every retarget task are in BL-2303's Context; BL-2309's Notes and ADR-0529 record the mapping.

Engine readers as of 2026-10-15: `EngineComposition.CreateFtpProtocolHandler` and `RoutingFtpProtocolHandler` (`-P`, `-Q`, `--ftp-*`, `--disable-epsv`/`--disable-eprt`, `-a`), `MailRequestOptionsMapping` (`--mail-from`, `--mail-rcpt`, `--mail-rcpt-allowfails`, `--mail-auth`), `SshOptionsMapping`, `SshKnownHostsFileSearch` and the SSH step of `TransferPerformer` (`--key`, `--pubkey`, `--hostpubmd5`, `--hostpubsha256`, `--knownhosts`, `--compressed-ssh`), and `TransferContextFactory`'s TFTP and TELNET fields (`--tftp-blksize`, `--tftp-no-options`, `-t`).

The known-hosts default search (`~/.ssh/known_hosts` when `--knownhosts` is not given) stays the tool's, per transfer, as ADR-0529 records; `-l`, `-B` and `--crlf` are the transfer group's (BL-2347). `CommandLineOptions` disappears from `Curl.Engine.UnitLibrary` only once BL-2343 to BL-2347 and this task are all Done. Tests that built `CommandLineOptions` only to drive these classes now build the records; tests through the whole command line stay as they are.

## Acceptance criteria

- [ ] `EngineComposition`'s FTP handler factory, `RoutingFtpProtocolHandler`, `MailRequestOptionsMapping`'s sender and recipients, `SshOptionsMapping`, `SshKnownHostsFileSearch`, `TransferPerformer`'s SSH step and `TransferContextFactory`'s TFTP and TELNET fields take `EasyProtocolOptions`, not `CommandLineOptions`, for these settings.
- [ ] `dotnet build` is clean (warnings are errors).
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Console,Curl.Engine.UnitLibrary` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes.
- [ ] `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.

## Notes

## Log

- 2026-10-15: Created.
