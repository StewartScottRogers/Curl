---
id: BL-2127
title: Curl reports itself as curl 8.22.0 in -V, the User-Agent, write-out, DICT and --ai-help
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2125]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests, Curl.Console.UnitTests, Curl.Http3.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Protocol.Dict.UnitLibrary, Curl.Protocol.Dict.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Protocol.Imap.UnitTests, Curl.Protocol.Rtsp.UnitLibrary, Curl.Protocol.Rtsp.UnitTests, Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests, Curl.Protocol.Ws.UnitLibrary, Curl.Protocol.Ws.UnitTests, Record-CurlExchange.ps1]
requirement: none
created: 2026-10-13
completed:
---
# BL-2127 — Curl reports itself as curl 8.22.0 in -V, the User-Agent, write-out, DICT and --ai-help

## Goal

Every version string Curl prints or sends says 8.22.0 where curl 8.22.0 does, measured from
the 8.22.0 reference before it is pinned.

## Context

ADR-0479 step 3.2 (retarget to curl 8.22.0). Waits on BL-2125, which makes the machine's
reference curl 8.22.0, so measure with `Record-CurlExchange.ps1` and `curl -V` first.

The literals on 2026-10-13: `CurlVersionText` (`-V` first line, three platform variants),
`LibcurlSourceCode.DefaultUserAgent` and `HttpProxyTunnelOptions.Default` (`curl/8.21.0`),
`TransferWriteOutVariables` (`libcurl/8.21.0 ...`), `DictRequest.ClientLine`
(`CLIENT libcurl 8.21.0`), `CurlAiHelpText` ("This `curl` is curl 8.21.0 ..."), and one
literal each in the Http, Rtsp, Tftp and Ws libraries. `git grep -n "8\.21\.0" -- "*.cs"`
filtered to non-comment lines gave 394 lines, most of them `User-Agent: curl/8.21.0` in tests
across the projects in `touches`. Prefer one shared version constant that the tests read over
394 edited literals, where the project references allow it without a protocol referencing
another protocol.

`UpstreamCaseRunner.CurlVersion` moves to 8.22.0 in the same commit: upstream cases expand
`%VERSION` from it (`User-Agent: curl/%VERSION`), so changing one without the other fails the
ratchet. Check whether 8.22.0's `-V` lines (Release-Date, Features, Protocols, the libraries
and their versions) differ from 8.21.0's beyond the number and pin what was measured.

Doc comments and ADRs that say "as curl 8.21.0 does" are statements of what was measured and
stay (ADR-0479 decision 4); only claims of what Curl is now change. `Record-CurlExchange.ps1`'s
help names the curl it finds; update it to 8.22.0.

## Acceptance criteria

- [ ] `curl -V` on Windows prints the first line the 8.22.0 Schannel reference prints, as recorded in this task's Notes, and the Linux and macOS variants say 8.22.0.
- [ ] A default request's `User-Agent` is `curl/8.22.0`, through a proxy tunnel too, and `--libcurl` output names it.
- [ ] `-w '%{json}'`'s `curl_version` member says `libcurl/8.22.0 ...` as the 8.22.0 reference prints it, and a DICT request sends `CLIENT libcurl 8.22.0`.
- [ ] `--ai-help` says this curl is curl 8.22.0.
- [ ] `UpstreamCaseRunner.CurlVersion` is `8.22.0`, and no non-comment C# line outside measurement fixtures names 8.21.0 as Curl's own version.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-10-13: Created.
