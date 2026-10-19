# Curl

<a href="https://stewartscottrogers.github.io/Curl/" target="_blank"><img src="https://github.com/StewartScottRogers/Curl/raw/gource/gource.gif" alt="Gource animation of Curl's commit history across every branch - click to watch in 4K, full screen" width="800"></a>

### [▶ Watch in 4K, full screen](https://stewartscottrogers.github.io/Curl/)

*Every commit on every branch, human and AI, drawn by [Gource](https://gource.io) at
3840 × 2160 and re-rendered after new commits and at least once a day. The viewer plays
the best quality your screen can show - 4K or HD - with a 4K MP4 and a 4K still to
download. Ctrl-click (⌘-click on a Mac) to open it in its own tab.*

### Code coverage

[![Code coverage: lines and branches covered across every production library - click for the full report](https://github.com/StewartScottRogers/Curl/raw/gource/coverage/badge.svg)](https://stewartscottrogers.github.io/Curl/coverage/)

*Every production library is held to 100% line and branch coverage, cyclomatic complexity
of at most 10 and a CRAP score of at most 30. The [full report](https://stewartscottrogers.github.io/Curl/coverage/)
shows each library against those gates and every member outside one, measured on Windows
and regenerated on the same schedule as the video above.*

### Integration tests

[![Integration tests: passed out of run on each platform - click for the runs](https://github.com/StewartScottRogers/Curl/raw/gource/integration/badge.svg)](https://github.com/StewartScottRogers/Curl/actions/workflows/integration.yml?query=branch%3Awork%2Fdark-factory)

*The tests marked `TestCategory("Integration")` - real loopback sockets, TLS servers and
disk files - run in CI on Windows, Linux and macOS, on every push and pull request, in
their own [Integration tests](.github/workflows/integration.yml) workflow beside the fast
tests' CI. The badge shows each platform's passed out of run for the latest finished run on
`work/dark-factory`, and is red when any failed. **They never fail a run yet**: a failure
is listed in the run's summary and on the badge but blocks nothing, until
`INTEGRATION_FAILURES_FAIL_THE_RUN` in that workflow is set to `'true'`.*

### [▦ Live task board](https://stewartscottrogers.github.io/Curl/board/)

*The [live task board](https://stewartscottrogers.github.io/Curl/board/) shows every task
by state and one card per dark factory lane, and refreshes itself every few minutes.*

## What this is

Curl is a port of the open-source [curl](https://curl.se) command-line tool to C# on
.NET 10, built to be a **drop-in replacement**: the same options, the same exit codes,
the same bytes on stdout and stderr, so an existing script cannot tell which binary it
invoked. It publishes as a single native-AOT executable with no runtime to install.

The port is behavioural, not a line-by-line translation of curl's C source. curl's
documentation, its manual page and its test suite are the specification; every protocol
is reimplemented against its RFCs in its own class library, behind interfaces that can be
driven from a unit test without touching a network. The only dependency is the .NET base
class library.

## Use it from .NET: `Libcurl` and `Curl.Api`

Curl is also becoming a library. Two public programming interfaces sit over one transfer
engine, the same engine the `curl` executable runs, so a program gets the same behaviour
as the command line ([ADR-0506](Documentation/Planning/Decisions/ADR-0506-libcurl-and-curl-api-over-one-transfer-engine.md),
[plan](Documentation/Planning/Curl-Api-And-libcurl.md)):

| Project | What it is for |
| --- | --- |
| [`Libcurl`](Libcurl) | libcurl's API in C#, call for call: `CurlGlobal.Init()`, `CurlEasy` with `SetOption`, `Perform` and `GetInfo`, and the multi, share, URL, MIME, header and WebSocket interfaces. For code ported from C, and the future home of a native C ABI built with native AOT. Tested by `Libcurl.UnitTests`. |
| [`Curl.Api`](Curl.Api) | The same engine written the way a .NET developer expects: `CurlClient`, `async` with `CancellationToken`, `Stream` bodies, exceptions, `IAsyncEnumerable` for WebSocket messages, and a `CurlHttpMessageHandler` for `HttpClient`. Tested by `Curl.Api.UnitTests`. |
| [`Curl.Api.UnitLibrary`](Curl.Api.UnitLibrary) | The seam both stand on: the engine contracts (`ICurlEngine`, `IEasyTransfer`), result codes (`CurlCode` and the rest), the option groups (`EasyHttpOptions`, `EasyTlsOptions` and the rest), `TransferInfo` and the callback delegates. Either API can be rewritten another way without touching the engine. |
| [`Curl.Engine.UnitLibrary`](Curl.Engine.UnitLibrary) | The transfer engine, moved out of `Curl.Console` so the executable and both APIs share it. |
| [`Curl.Mcp`](Curl.Mcp) | A Model Context Protocol server over stdio, so AI agents (GitHub Copilot, Claude and others) use Curl as a tool: `curl`, `transfer`, `parse_url`, `build_url` and `version`, plus the `--ai-help` text as resources. File access is confined to `CURL_MCP_ROOT`. Run it with `dnx Curl.Mcp`; setup is in [its README](Curl.Mcp/README.md). Tested by `Curl.Mcp.UnitTests` and `Curl.Mcp.IntegrationTests`. |
| [`Curl.Mcp.UnitLibrary`](Curl.Mcp.UnitLibrary) | All of `Curl.Mcp`'s tool logic, base class library only: the path confinement, the tool implementations, the result and error types. |

Where it stands: the engine is extracted and `Curl.Api.UnitLibrary`'s seam is defined.
`Libcurl` and `Curl.Api` are implemented, not empty: `Libcurl` holds the global and easy
interfaces, the option table and setters, multi, share and MIME; `Curl.Api` holds
`CurlClient` and `CurlHttpMessageHandler`. `Curl.Conformance.LibTest.UnitLibrary` ports
upstream's `tests/libtest` cases to drive `Libcurl` and measure it. Both are held to the
same quality gates as every library here.

## Built by a dark factory

The port is being written by a *dark factory* - an unattended production line of Claude
Code agents built for this one job, porting curl, and not a general-purpose coding bot.

1. **Plan.** The work is decomposed into small tasks on the [task board](Tasks/README.md),
   one Markdown file per task, each with dependencies and checkable acceptance criteria.
2. **Run.** `RunDarkFactory.cmd` takes the next ready task and hands it to a headless
   Claude Code run that is not allowed to ask a question. It repeats until nothing is
   ready or the shift ends.
3. **Specialise.** Each run uses agents shaped for a curl port, in `.claude/agents`:
   a protocol architect and implementer, a test writer, a build fixer, a code reviewer,
   a coverage auditor, and a conformance auditor that checks options, exit codes and
   output bytes against real upstream curl.
4. **Gate.** Nothing lands unless it builds with warnings as errors and holds every
   library to 100% line and branch coverage, cyclomatic complexity of at most 10 and a
   CRAP score of at most 30.
5. **Escalate.** Anything that needs a human decision is moved to `Blocked` with the
   question written down, and the shift ends with an alarm until someone answers it.

The lights stay off; a person sets direction and answers blocked questions.

## Download

Native binaries for Windows, Linux and macOS, on x64 and Arm64, are on the
[**download page**](DOWNLOAD.md), with one-line installers:

```sh
curl -fsSL https://raw.githubusercontent.com/StewartScottRogers/Curl/master/install.sh | sh    # Linux, macOS
```

```powershell
irm https://raw.githubusercontent.com/StewartScottRogers/Curl/master/install.ps1 | iex          # Windows
```

## Build and test

Needs the .NET 10 SDK. Builds and tests on Windows, Linux and macOS.

```
dotnet build
dotnet test --filter "TestCategory!=Integration"
```

The Integration tests, as CI runs them (they open loopback sockets and write temporary files):

```
dotnet test --filter "TestCategory=Integration"
```

Run a factory shift (Windows):

```
RunDarkFactory.cmd -Hours 4 -MaxTasks 3
```

## Read more

- [Download and install](DOWNLOAD.md) - every supported platform, installers and checksums
- [Product overview](Documentation/Product/Product-Overview.md) - scope, architecture and roadmap
- [Libcurl and Curl.Api plan](Documentation/Planning/Curl-Api-And-libcurl.md) - the two .NET APIs, their phases and their libcurl inventory
- [Task board](Tasks/README.md) - what is being worked on, one Markdown file per task

## Licence

The curl licence (SPDX `curl`), the same licence curl itself uses; see [LICENSE.txt](LICENSE.txt) and [ADR-0486](Documentation/Planning/Decisions/ADR-0486-relicense-from-mit-to-the-curl-licence.md).
