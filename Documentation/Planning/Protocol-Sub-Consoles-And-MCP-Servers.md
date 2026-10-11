# Protocol sub-consoles with matching MCP servers

**Status: proposal, not started.** Nothing in this document exists in the code yet.
It is the reference for when Stewart asks for the work to begin. Estimates date from
2026-10-11.

## The idea

One executable per protocol, each a subset of Curl:

- `curl-ftp`, `curl-smtp`, `curl-http` and the rest, one per protocol library (17 today:
  Dict, File, Ftp, Gopher, Http, Imap, Ldap, Mqtt, Pop3, Rtsp, Smb, Smtp, Ssh, Telnet,
  Tftp, Ws, plus whatever lands later).
- Each contains only its own protocol library and the shared core (connection, TLS,
  authentication, output). Native AOT then trims every other protocol out of the binary.
- Each accepts curl's command line for its own options, byte for byte: same options,
  same output, same exit codes.
- Each has its own `--help`, `--ai-help` and `--manual`, covering only that protocol's
  options and the global ones.
- A URL or option for another protocol behaves as it does in a curl built without that
  protocol.
- Each also runs as an MCP server for AI agents, started with a flag on the same
  executable (`curl-ftp --mcp`, stdio transport), so the console and its MCP server never
  drift apart.

## What the code already gives us

| Fact | Where | Why it helps |
|---|---|---|
| Every protocol is registered in one place, as a `LazyProtocolHandler` per scheme set | `Curl.Console/CurlComposition.cs`, lines 144-161 at the time of writing | A sub-console is mostly this list made shorter |
| Protocol libraries never reference each other; the build fails if one does | CLAUDE.md, solution-wide conventions | Leaving a protocol out cannot break another |
| Help categories copy curl 8.21.0's `CURLHELP_*` bits (`http`, `ftp`, `file`, `smtp`, ...) | `Curl.Cli.UnitLibrary/CurlHelpCategories.cs` | Help filtering has a starting point |
| `--ai-help` is Markdown help for agents, by category | `Curl.Cli.UnitLibrary/CurlAiHelpText.cs` (BL-911) | Gives MCP tool descriptions for free |
| A whole curl command line runs in process | `InProcessCurl.RunAsync` (used by `UpstreamConformanceTests`) | An MCP tool call can reuse the engine without starting a process |
| Upstream curl builds without a protocol with `--disable-<protocol>` | curl's `configure` | A real reference binary to measure each sub-console against |

## Part 1: the sub-consoles

### Work

| Piece | Tasks | Difficulty |
|---|---|---|
| Composition takes a protocol set, plus one pilot (`curl-ftp`) end to end | 10-15 | Easy to medium |
| Tag every option (about 270) with the protocols it applies to, generated from the "Protocols:" lines in upstream `docs/cmdline-opts/*.md` | ~5 | Medium |
| Filtered `--help`, `--ai-help`, `--manual`, and `-V`'s Protocols and Features lines | ~5 | Easy |
| Match curl's behaviour for a left-out protocol or option ("Protocol "x" not supported", "the installed libcurl version does not support this", exit codes) | ~5 | Medium |
| The other 16 sub-consoles, once the pilot works | ~40 | Easy |
| CI and publishing (17 native AOT publishes), and gap office reference builds of curl with protocols disabled | ~10 | Medium; slowest to run |

**Total: about 70-90 tasks, 4-6 dark factory shifts, 2-4 days of wall time.**

### Hard parts

1. **Filtered help is a departure from curl.** Real curl's `--help` lists every option
   whatever its build leaves out; a left-out option fails only when used. A filtered help
   page therefore matches no curl build. See decision 1.
2. **Options are not tagged per protocol yet.** `connection`, `tls`, `auth` and `proxy`
   categories span many protocols, so each option needs an explicit protocol list.
3. **Protocols lean on each other.** WebSocket and RTSP need HTTP; FTP, IMAP and others
   tunnel through HTTP proxies (CONNECT); redirects cross protocols (HTTP to IMAP).
   Default: do what curl does - `--disable-http` also removes HTTP proxies.
4. **Build and CI cost grows with every executable.** Each sub-console is a native
   publish, and each is held to the quality gates.

## Part 2: the MCP servers

### Work

| Piece | Tasks | Difficulty |
|---|---|---|
| Shared `Curl.Mcp.UnitLibrary`: JSON-RPC over stdio, lifecycle (`initialize`, `tools/list`, `tools/call`), tool schemas, progress and cancellation notifications, AOT-safe `System.Text.Json` source generation | 10-15 | Medium |
| Safety controls: allowed hosts, an output folder, whether local file reads (`-T`, `-F @file`, `-d @file`) and credential options are allowed, size caps on returned bodies | 5-8 | Medium; mostly design |
| **Thin** tools: one `<protocol>_run` tool per sub-console taking curl arguments, its description taken from that protocol's `--ai-help` | ~17 | Easy |
| **Or rich** tools: typed tools such as `ftp_list`, `ftp_get`, `smtp_send`, `imap_fetch`, with JSON schemas | 50-85 | Medium |
| CI smoke test per server: start it, check `tools/list`, run one `tools/call` against a loopback server | ~5 | Easy |

**Thin: about 35-45 tasks, about 2 shifts after the sub-consoles.**
**Rich: about 70-110 tasks, about 4-6 shifts.**

### Hard parts

1. **No package.** Microsoft's C# MCP SDK is a NuGet package; the base-class-library-only
   rule means hand-building the MCP layer unless Stewart approves the package (decision 3).
2. **Safety.** An agent with curl can reach any host, write files, send credentials and
   upload local files. Each server needs limits, with careful defaults.
3. **Output.** Binary bodies need base64 or resource links; large transfers need caps;
   long transfers need progress and cancellation passed through.
4. **No reference to compare against.** The consoles can be measured byte for byte against
   real curl; the MCP layer only against the MCP specification, with hand-written tests.

## Decisions

### Stewart's (ask before starting)

1. **Filtered help or curl-matching help?** Filtered shows only the protocol's options but
   matches no real curl. Matching keeps each sub-console a drop-in for a curl built with
   only that protocol.
2. **Thin or rich MCP tools?** Recommendation: thin first, rich later for the protocols
   agents actually use.
3. **Hand-built MCP layer or Microsoft's MCP SDK package?** Hand-built is the standing rule
   and the default; the package needs explicit approval.
4. **What it is for** - smaller binaries, simpler help for people or agents, or separately
   shipped products - since each changes the design a little.

### Claude's (decide under the delegation, record as ADRs)

- Executable and project names (the `Curl.Console` naming exception needs extending).
- One project per sub-console, or one project published 17 ways with an MSBuild property.
  The protocol set must be fixed at build time, not by a runtime flag, or AOT keeps every
  protocol in the binary.
- Which shared pieces each sub-console carries (HTTP proxy, redirects, authentication).
- The MCP flag's name and the safety defaults.

## How to start

1. Stewart answers decisions 1-4.
2. Claude writes the ADRs for its own decisions.
3. `task-planner` files Part 1 as tasks, pilot first (`curl-ftp`), the other 16 depending on it.
4. Part 2 is filed once the pilot sub-console works, MCP library first, then one server per
   sub-console.
5. The dark factory works the tasks; the gap office gains a per-sub-console reference measurement.
