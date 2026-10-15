# ADR-0506 — `Libcurl` and `Curl.Api`: two public .NET APIs over one transfer engine

- **Status:** Accepted
- **Date:** 2026-10-15

The project split - two public APIs, `Libcurl` and `Curl.Api`, over one engine, with
`Curl.Console` re-based onto `Libcurl` - was **approved by Stewart on 2026-10-15** from
Claude's recommendation. The libcurl-faithful project's name, `Libcurl`, is Stewart's:
**renamed from `Curl.libcurl` by Stewart 2026-10-15**, to match upstream's library name. The
other details below (where the engine lives, the seam, the other names, sorting, test placement) are **decided by Claude under Stewart's delegation** (root `CLAUDE.md`,
"Decisions"). The plan and its phases are `Documentation/Planning/Curl-Api-And-libcurl.md`.

## Context

Curl is a drop-in replacement for the curl tool. Upstream curl is two things: libcurl, the
library every transfer runs in, and the curl tool, which turns a command line into
`curl_easy_setopt` calls (`tool_operate.c`, `config2setopts.c`) and handles what only a command
line has - output files, globbing, the progress meter, `-w`, `--retry`, `--parallel`.

Curl has the tool and no library. Its transfer engine lives in `Curl.Console`:

- `CurlCommandRunner.cs` (~290 KB, ~4900 lines) does both halves at once: tool work (parse,
  glob, output files, progress meter, `-w`, retry, parallel scheduling) and library work (URL
  parse and refusals, proxy and credential selection, cookies, HSTS, alt-svc, redirects,
  watchdogs), all keyed on `CommandLineOptions` from `Curl.Cli.UnitLibrary`.
- `CurlComposition.cs` (~110 KB) is the composition root: protocol handlers, transports, DNS
  resolvers, TLS providers, security-context factories, every one built from
  `CommandLineOptions`.
- `TransferContextFactory` builds each `TransferContext` and `TransferDispatch` holds a run's
  dispatcher, cookies, proxy selector and connection pool. `CurlCommandRunner` already takes a
  `Func<CommandLineOptions, TransferDispatch>`, which is the nearest thing to a seam today.
- About 40 more files in `Curl.Console` are library work (`CookieEngine`, `AltSvcTransferCache`,
  the `*Mapping` classes, the trace-event decorators, the authenticators).

No .NET program can use Curl's transfers without running `curl` as a process, and upstream's
362 `libcurl-api` test cases (each a C program in `tests/libtest`) cannot be measured at all; the
gap office excludes them (ADR-0433).

## Decision

### Projects

| Project | Kind | Holds |
| --- | --- | --- |
| `Curl.Engine.UnitLibrary` | library | The managed transfer engine: everything in `Curl.Console` that libcurl does in upstream, moved out of it. Implements the engine contracts. References the protocol libraries and the existing `Curl.*` libraries. |
| `Curl.Engine.UnitTests` | tests | Its tests; tests of moved classes move here with them. |
| `Curl.Api.UnitLibrary` | library | The seam: the engine contracts both APIs call (`Curl.Api.EngineContracts`), and the vocabulary both expose - result codes (`Curl.Api.Codes`), the easy option groups (`Curl.Api.Options`), the transfer info read model (`Curl.Api.Info`) and the callback delegates (`Curl.Api.Callbacks`). References no protocol library and no engine. |
| `Curl.Api` | public API | The .NET-normal API, thin: `CurlClient` (constructed, `IAsyncDisposable`), async with `CancellationToken` everywhere, `Stream` bodies, option records, `CurlException` carrying the `CurlCode`, `IAsyncEnumerable` WebSocket messages, and `CurlHttpMessageHandler : HttpMessageHandler`. |
| `Curl.Api.UnitTests` | tests | Tests of **both** `Curl.Api` and `Curl.Api.UnitLibrary`. |
| `Libcurl` | public API | The libcurl-faithful API, thin: `CurlGlobal.Init()`, `CurlEasy`, `CurlMulti`, `CurlShare`, the URL, MIME, form, WebSocket and header APIs, version info, escape, getdate, slist, strerror - every `CURLOPT_*` and `CURLINFO_*` by name. |
| `Libcurl.UnitTests` | tests | Its tests. |

**Why a dedicated engine library rather than the engine inside `Curl.Api.UnitLibrary`.**
Stewart's approved shape allowed either. The code decides it:

1. *The seam must be replaceable on its own.* A future engine over native libcurl (P/Invoke)
   implements the contracts and nothing else. If the contracts and the managed engine shared an
   assembly, that engine would still reference all 17 protocol libraries, TLS, QUIC and SSH. With
   the contracts alone in `Curl.Api.UnitLibrary`, replacing the engine is one new project
   implementing one set of interfaces.
2. *The engine is mostly existing code moving house*, about 400 KB of it, with its tests. Keeping
   it in its own project keeps that move separate from the new public vocabulary, so the move's
   tasks (`touches: Curl.Console, Curl.Engine.*`) and the vocabulary's tasks
   (`touches: Curl.Api.UnitLibrary/<folder>`) run in parallel dark factory lanes.
3. *`Curl.Core.UnitLibrary` cannot be the engine*: it must never reference a protocol library
   (root `CLAUDE.md`), and the engine composes all of them.

### Layering

```
 Curl.Console (the curl tool)     .NET programs            C programs (future)
        |                         |          |                    |
        v                         v          v                    v
   Libcurl  <---------  (either)   Curl.Api        libcurl.dll/.so exports (future,
        |                                    |          NativeAOT over Libcurl)
        +------------------+-----------------+
                           v
              Curl.Api.UnitLibrary  (the seam: engine contracts + shared vocabulary)
                           ^
                           | implements
              Curl.Engine.UnitLibrary  (managed engine)
                           |
     Curl.Core, Curl.Networking, Curl.Tls, Curl.Authentication, Curl.Cookies, Curl.Output,
     Curl.Protocol.Abstractions, Curl.Protocol.<17 protocols>, Curl.Http2, Curl.Http3, ...
```

- `Libcurl` and `Curl.Api` reference `Curl.Api.UnitLibrary` and never each other; neither
  holds transfer logic. Each needs an engine at run time: `CurlGlobal.Init()` and the
  `CurlClient` constructor take an `ICurlEngine`, with a parameterless overload that builds the
  managed one - by a plain constructor call, since static service locators are banned.
- `Curl.Console` ends up consuming `Libcurl` as the curl tool consumes libcurl. On the way
  there it consumes `Curl.Engine` directly (phase 1) and then the seam (phase 2); the switch to
  `Libcurl` is a phase 3 task.
- What the curl tool does and libcurl does not stays in `Curl.Console`: argument parsing (in
  `Curl.Cli`), globbing, `-o`/`-O`/`-J` output files, `--create-dirs`, the progress meter and
  bar, `-w` formatting, `--retry`, `--parallel` scheduling, `--etag-*`, IPFS rewriting,
  `--libcurl`, `--xattr`, `Warning:` wrapping and the `curl: (N)` lines.

### The engine's input is a CLI-neutral option model

Today the engine reads `CommandLineOptions`. It moves first with that dependency intact (a
mechanical move, so behaviour cannot change), then each option group is retargeted to an
**easy option group** record in `Curl.Api.Options` - one record per `curl_easy_setopt` group
(behaviour and transfer, network and connection, TLS, authentication, proxy, HTTP, the other
protocols, callbacks), each property documented with its `CURLOPT_*` name and libcurl's default.
`Curl.Console` gains the mapping from `CommandLineOptions` to those records, as `config2setopts.c`
maps `struct OperationConfig` to `curl_easy_setopt`; `Curl.Cli`'s `LibcurlSourceCode`, which
already names the `CURLOPT_*` each command-line option sets for `--libcurl`, is the reference for
that mapping. When the last group is retargeted the engine drops its `Curl.Cli` reference, and the
build proves the engine is CLI-free.

`Libcurl`'s `SetOption(CurlOption.X, value)` writes into the same records through one table
(the `options.h` metadata: name, id, type), and `Curl.Api`'s option records map onto them, so all
three front ends drive the engine through one model.

### Names

- **`CurlCode` and `CurlExitCode` are two concepts.** `CurlCode` (in `Curl.Api.Codes`) is a
  libcurl function's result, `CURLcode`, and the public APIs use it. `CurlExitCode` (in
  `Curl.Protocol.Abstractions`) stays the engine-internal outcome and the curl tool's process exit
  status, which upstream also derives from the failing transfer's `CURLcode`. Their numbers are
  equal member for member, a test pins that, and the seam converts by number. Renaming
  `CurlExitCode` solution-wide would touch every protocol library for no behaviour; it is not done.
  The other code enums are `CurlMultiCode`, `CurlShareCode`, `CurlUrlCode`, `CurlHeaderCode` and
  `CurlFormAddCode`, all defined once in `Curl.Api.Codes` and exposed by both APIs.
- **`Libcurl` is exactly that** - no `Curl.` prefix, capital L, the rest lower case - Stewart's
  choice (renamed from `Curl.libcurl` on 2026-10-15), matching upstream's library name. Its tests
  are `Libcurl.UnitTests` and its root namespace is `Libcurl`. A type there that would clash with an existing Curl type takes a
  suffix rather than a different word: the URL handle is `CurlUrlHandle`, because `CurlUrl`
  already names Curl's parsed URL.
- **`Curl.Api.UnitLibrary`'s root namespace is `Curl.Api`** by `Directory.Build.props`' rule, the
  same as `Curl.Api`'s. The seam's types sit in sub-folders, so their namespaces are
  `Curl.Api.EngineContracts`, `Curl.Api.Codes`, `Curl.Api.Options`, `Curl.Api.Info` and
  `Curl.Api.Callbacks`; `Curl.Api`'s own public types (`CurlClient` and the rest) are in
  `Curl.Api`. Seam folders also let option-group tasks name disjoint `touches` and run in parallel.
- The seam's option records are `Easy<Group>Options` (`EasyNetworkOptions`, `EasyTlsOptions`, ...),
  so they never clash with the protocol libraries' `HttpRequestOptions`, `TlsClientOptions` and the
  like that the engine maps them onto.

### Naming exceptions, sorting and tests

- `Curl.Api` and `Libcurl` are, like `Curl.Console`, exceptions to the `Curl.<Area>.UnitLibrary`
  naming rule: they are public faces, not libraries of logic, and `Libcurl` also drops the `Curl.`
  prefix. They are held to the same four quality gates
  as every `*.UnitLibrary`: 100% line, 100% branch, cyclomatic complexity at most 10, CRAP at most
  30. `Measure-CodeQuality.ps1` counts them as production code.
- `Curl.Api.UnitTests` tests both `Curl.Api` and `Curl.Api.UnitLibrary`. It sorts directly after
  `Curl.Api.UnitLibrary`, which sorts directly after `Curl.Api`, so the sorting rule holds without a
  name that breaks the pattern; the seam is small (contracts, enums, records) and its tests sit
  naturally beside the wrapper's.
- In the flat alphabetical run, `Libcurl` and `Libcurl.UnitTests` sort after every `Curl.*`
  project.
- The scaffolding task (BL-2292) updates root `CLAUDE.md`'s naming section when the projects
  exist, saying `Curl.Console`, `Curl.Api` and `Libcurl` are the exceptions to the
  `Curl.<Area>.UnitLibrary` rule; nothing there names them before then.

### Packages and native AOT

- Base class library only. `Curl.Api` takes no `Microsoft.Extensions.*` package; a dependency
  injection registration helper is not part of it (not approved).
- Every new production project is AOT-compatible by `Directory.Build.props`; no reflection, and
  the option table in `Libcurl` is a hand-written switch or array, never attribute scanning.
- The NativeAOT C ABI - `libcurl.dll`/`libcurl.so` exporting `curl_easy_*` over `Libcurl` -
  is future work and a separate request; no task is filed for it. `Libcurl`'s shape (handles
  as objects, codes as the upstream numbers, callbacks as delegates) is chosen so that layer can be
  added by `[UnmanagedCallersOnly]` exports without changing it.

### What must not change

Every refactor task of the extraction keeps every existing test (moved, never deleted or
weakened), the upstream-case ratchet (`Curl.Conformance.UnitTests/PassingUpstreamCases.txt`)
unchanged, and the gap office's scores unchanged. Two gap-office dependencies on `Curl.Console`
paths constrain the move, since `Gap/` changes only on the `gap` branch from an interactive
session:

- `Gap/Tools/Measure-ExitCodeGap.ps1` reads `Curl.Console/CurlEasyErrorText.cs` by path, so that
  file stays where it is in phases 1 and 2; the engine receives the error text it needs (DoH) as a
  `Func<CurlExitCode, string>`. Moving it (it is `curl_easy_strerror`'s table) is a phase 3 task
  paired with an interactive `Gap/` change.
- `Gap/Tools/Measure-UpstreamCases.cs` calls `Curl.Console.InProcessCurl.RunAsync`, so its public
  signature stays.

## Consequences

- .NET programs can run Curl's transfers in process, through a libcurl-faithful API or a .NET one,
  and `HttpClient` code can use Curl as its transport.
- The 362 `libcurl-api` upstream cases become measurable once their `tests/libtest` programs are
  ported to C# against `Libcurl` (phase 5); the gap office's part of that is interactive only.
- For the length of phase 1 the extraction tasks overlap on `Curl.Console` and run one at a time;
  the seam's tasks run beside them.
- `Curl.Console` shrinks to the tool, which makes the protocol sub-consoles
  (`Documentation/Planning/Protocol-Sub-Consoles-And-MCP-Servers.md`) a smaller job.
