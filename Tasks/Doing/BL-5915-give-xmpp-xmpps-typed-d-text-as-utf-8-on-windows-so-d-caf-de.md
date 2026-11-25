---
id: BL-5915
title: Give xmpp / xmpps typed -d text as UTF-8 on Windows, so -d café delivers café
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineOptions.cs, Curl.Cli.UnitTests, Curl.Console/EasyOptionsFromCommandLine.cs, Curl.Console.UnitTests, Libcurl, Libcurl.UnitTests, Curl.Engine.UnitLibrary/EngineTransferSettings.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Context.cs, Curl.Engine.UnitLibrary/TransferContextFactory.cs, Curl.Engine.UnitTests, Curl.Protocol.Abstractions.UnitLibrary/ITransferContext.cs, Curl.Protocol.Abstractions.UnitLibrary/TransferContext.cs, Curl.Protocol.Abstractions.UnitTests, Curl.Extended.Protocol.Xmpp.UnitLibrary/Client, Curl.Extended.Protocol.Xmpp.UnitTests, Documentation/Conformance/Extended/Xmpp.md]
requirement: none
created: 2026-11-24
completed:
---
# BL-5915 — Give xmpp / xmpps typed -d text as UTF-8 on Windows, so -d café delivers café

## Goal

On Windows, `curl -d café xmpp://...` delivers the message body `café`.

## Context

Found by BL-5899 (EXT-124), measured 2026-11-24 on Windows (ANSI code page 1252): `curl -sS -d 'café' xmpp://127.0.0.1:1/carol@localhost`
exits 43, `XMPP message body is not valid UTF-8` (ADR-1350), while `-d @utf8file` goes on to connect. A typed argument
becomes bytes in the ANSI code page (`CommandLineOptions.CommandLineTextBytes`, ADR-0838), which is right for curl's own
protocols and must not change for them. Before BL-5899 the same command silently delivered `caf` plus U+FFFD.

The handler sees only `ITransferContext.PostData`, so it cannot tell typed text from a file's bytes. One way: the command
line also keeps the body with each typed piece spelled in UTF-8 and passes it as an optional context member, which the
XMPP handler prefers. The path to the handler is `EasyOptionsFromCommandLine`, `ManagedEasyTransfer.Context.cs`,
`EngineTransferSettings`, `TransferContextFactory`. Narrow `touches` once the design shows the files. Bytes from a file
(`-d @file`, `--data-binary @file`, `-T`) stay as they are and are still refused when not UTF-8.

## Acceptance criteria

- [ ] On Windows, typed `-d café` to `xmpp://` sends `<body>café</body>` in UTF-8, pinned by a unit test.
- [ ] A body from a file that is not valid UTF-8 (`-T`, `--data-binary @file`) still exits 43 and sends nothing.
- [ ] The bytes `-d café` sends over `http://` are unchanged on each platform.
- [ ] `Documentation/Conformance/Extended/Xmpp.md` says what is true after the change.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing. Criteria at claim: a04963e0 ab56beea c8baba98 5da11203 3d836d5e.
