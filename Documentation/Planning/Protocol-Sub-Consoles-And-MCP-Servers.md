# Protocol sub-consoles with matching MCP servers and skills

**Status: proposal, not started; decisions 1-4 and 7-10 made, 5 and 6 open.** Nothing in this document exists in the code yet.
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
- Each has its own filtered `--help`, `--ai-help` and `--manual`, covering only that
  protocol's options and the global ones (decision 1, decided by Stewart 2026-10-11).
  `curl-ftp --help all` lists about 80-100 options, not curl's ~270.
- A URL or option for another protocol behaves as it does in a curl built without that
  protocol.
- Each also runs as an MCP server for AI agents, wrapped in a Claude Code skill (Part 3), started with a flag on the same
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
| Filtered `--help` (every form: the default page, `all`, and each category), `--ai-help`, `--manual`, and `-V`'s Protocols and Features lines, each pinned by its own tests since no real curl prints them | ~8 | Easy to medium |
| A check that every option's protocol tag is right: each option a sub-console's help lists must work there, and each it leaves out must be one curl would ignore or refuse there | ~3 | Medium |
| Match curl's behaviour for a left-out protocol or option ("Protocol "x" not supported", "the installed libcurl version does not support this", exit codes) | ~5 | Medium |
| The other 16 sub-consoles, once the pilot works | ~40 | Easy |
| CI and publishing (17 native AOT publishes), and gap office reference builds of curl with protocols disabled | ~10 | Medium; slowest to run |

**Total: about 75-95 tasks, 4-6 dark factory shifts, 2-4 days of wall time.**

### Hard parts

1. **Filtered help is a deliberate departure from curl (decision 1).** Real curl's
   `--help` lists every option whatever its build leaves out; a left-out option fails only
   when used. So a sub-console's help output matches no curl build:
   - The gap office cannot measure help output against a reference; it is pinned by
     Curl's own tests instead, and the gap office excludes it with that reason.
   - Everything except help - option parsing, output bytes, exit codes - still matches a
     curl built with only that protocol and is measured against one.
   - The sub-console's help says so, so nobody mistakes it for curl's: the departure is
     recorded in an ADR, and the full `curl` executable keeps curl's unfiltered help.
2. **Options are not tagged per protocol yet.** `connection`, `tls`, `auth` and `proxy`
   categories span many protocols, so each option needs an explicit protocol list. With
   filtered help a wrong tag is visible to users: it hides an option that works or shows
   one that does nothing.
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
| Simple tools (decision 2): one `<protocol>_run` tool per sub-console, taking curl's arguments for that protocol and returning exit code, stdout and stderr; its description is that protocol's filtered `--ai-help` | ~17 | Easy |
| CI smoke test per server: start it, check `tools/list`, run one `tools/call` against a loopback server | ~5 | Easy |

**Total: about 35-45 tasks, about 2 shifts after the sub-consoles.**

Typed tools (`ftp_list`, `ftp_get`, `smtp_send`, `imap_fetch` and the like, each with its
own JSON schema) are left out by decision 2. Adding them later for one protocol is about
3-5 tasks on top of its simple tool, which stays.


### Hard parts

1. **Hand-built MCP layer (decision 3).** There is no package: framing, the `initialize`
   version negotiation and cancellation are written and tested here, and each MCP
   specification revision is followed by hand. The scope is small - stdio only, one tool per
   server - which is what keeps this cheap.
2. **Safety.** An agent with curl can reach any host, write files, send credentials and
   upload local files. Each server needs limits, with careful defaults, since the servers
   ship publicly (decision 4).
3. **Output.** Binary bodies need base64 or resource links; large transfers need caps;
   long transfers need progress and cancellation passed through.
4. **No reference to compare against.** The consoles can be measured byte for byte against
   real curl; the MCP layer only against the MCP specification, with hand-written tests.

## Part 3: the skills and the plugin

Each protocol gets a Claude Code skill (`SKILL.md`) wrapped around its MCP server, and all
of them ship together as one Claude Code plugin, `curl-protocols` (decision 9).

- **The skill's job:** its frontmatter description tells an agent when to use the protocol;
  its body says how to use it well - recipes, safety rules, what each exit code means and
  what to do next. Only the one-line description is always in context; the body loads when
  a task needs that protocol. So the MCP tool's own description stays short, and the
  detailed help lives in the skill.
- **MCP or command line (decision 10):** the skill uses the `<protocol>_run` MCP tool when
  the server is connected and falls back to running the sub-console on the command line
  when it is not.
- **Kept true:** each skill's option reference is generated from its sub-console's filtered
  `--ai-help`; only the recipes are written by hand, and a test fails the build when a skill
  names an option its sub-console does not have.

### The plugin

| Piece | Choice |
|---|---|
| Where it is published | A plugin marketplace file in the Curl repository (`.claude-plugin/marketplace.json`); the repository is public under the MIT licence |
| What it holds | The skills (one per sub-console) and each MCP server's configuration |
| The binaries | Native `curl-ftp`, `curl-http` and the rest for Windows, Linux and macOS, downloaded from GitHub Releases (decision 7) |
| Versioning | One version for the whole plugin, with a changelog per version |

One plugin, not one per protocol: a single install gives everything, unused skills cost
almost nothing in context, and one version number is what Surl pins.

### Surl

Surl (<https://github.com/StewartScottRogers/Surl>, curl's server-side counterpart) uses
the plugin as its test client:

1. Surl's `.claude/settings.json` adds the Curl marketplace and enables `curl-protocols`,
   so every Surl session, its dark factory lanes included, has it without a manual install.
2. Surl pins a plugin version, so a change in Curl never breaks Surl's tests unannounced;
   Surl moves to a new version on purpose.
3. A Surl session starts a Surl server and drives it with that protocol's `<protocol>_run`
   tool, with the skill telling the agent how to read the result.

Enabling the plugin in Surl and writing Surl's tests with it are Surl tasks, not Curl tasks.

### Work

| Piece | Tasks | Difficulty |
|---|---|---|
| Skill generator: frontmatter, and an option reference built from the filtered `--ai-help` | 3-5 | Easy |
| A test that fails when a skill names an option its sub-console lacks | 2-3 | Easy |
| Hand-written recipes and safety notes, one per protocol, checked against real curl | ~17 | Easy, but needs real measurement |
| Plugin and marketplace files, an install check in CI | 3-5 | Easy to medium |
| Release workflow publishing the binaries (decision 7), plugin README, changelog | 5-7 | Medium |

**Total: about 33-47 tasks, about 1-2 shifts after the MCP servers.** For only the
first protocols (decision 6), about 15-20.

## Decisions

| # | Decision | Answer | Decided |
|---|---|---|---|
| 1 | Help | Filtered | Stewart, 2026-10-11 |
| 2 | MCP tools | Simple: one `<protocol>_run` tool per server | Stewart, 2026-10-11 |
| 3 | MCP layer | Hand-built, no package | Stewart, 2026-10-11 |
| 4 | Purpose | Public distribution, and the test client for Surl | Stewart, 2026-10-11 |
| 5 | When it starts | Open | - |
| 6 | Which protocols first | Open | - |
| 7 | How they ship | GitHub Releases under a standing exception, CI green on all three platforms | Stewart, 2026-10-11 |
| 8 | Who reaches the MCP servers | Local stdio only | Stewart, 2026-10-11 |
| 9 | Packaging | One Claude Code plugin, `curl-protocols`, published from the Curl repository | Stewart, 2026-10-11 |
| 10 | A skill without MCP | Falls back to the sub-console's command line | Stewart, 2026-10-11 |

Decisions 3 and 7-10 are Claude's recommendations, which Stewart accepted on 2026-10-11.

### Stewart's, with reasons

1. **Filtered help.** Each sub-console's `--help` (every form), `--ai-help`, `--manual` and
   MCP tool descriptions list only its own protocol's options and the global ones.
   Considered and not chosen: help matching curl byte for byte (a drop-in for a reduced curl
   build, but listing options that do nothing there), and a hybrid that kept `--help`
   matching curl while filtering only `--ai-help`.
2. **Simple MCP tools.** One tool, `<protocol>_run`, that takes curl arguments, so the MCP
   server does exactly what its sub-console does and nothing else. Typed tools per
   operation can be added later for the protocols agents use most.
3. **Hand-built MCP layer.** It keeps the base-class-library-only rule, gives public users
   the smallest native binaries with no runtime or extra packages to trust, and keeps all of
   it under the quality gates. Microsoft's MCP SDK package becomes the better choice only if
   the servers later need network transport or richer MCP features.
4. **Purpose.** Public distribution, and Stewart's own use as the test client when testing
   the Surl repository.
7. **Releases under a standing exception.** CI publishes a GitHub Release of the binaries
   and the plugin when a sub-console or plugin version changes, only when CI passed on
   Windows, Linux and macOS for that commit. The exception takes effect when it is written
   into CLAUDE.md's "Git and GitHub" section at the start of Part 3; until then every
   release or tag is asked for.
8. **Local stdio only.** Public users and Surl both run the servers on their own machines,
   so nothing is exposed to the network and no authentication is needed.
9. **One plugin.** One install, one version for Surl to pin, and unused skills cost almost
   nothing in context.
10. **Command-line fallback.** The skills work in Claude Code even when MCP is not connected.

### Still to ask before starting

5. **When it starts** - before or after the gap and audit work on the board; the dark
   factory works one board, so this sets the order. Recommendation: after it clears.
6. **Which protocols first** - all 17, or a pilot (`curl-ftp`) and then HTTP, SMTP, IMAP
   and SFTP, with the other 12 waiting until asked. Recommendation: the pilot and those
   four, about half of Part 1's work.

### Claude's (decide under the delegation, record as ADRs)

- Executable and project names (the `Curl.Console` naming exception needs extending).
- One project per sub-console, or one project published 17 ways with an MSBuild property.
  The protocol set must be fixed at build time, not by a runtime flag, or AOT keeps every
  protocol in the binary.
- Which shared pieces each sub-console carries (HTTP proxy, redirects, authentication).
- How sub-consoles group schemes (whether HTTPS, WebSocket and HTTP/3 belong in
  `curl-http`, SCP and SFTP in `curl-ssh`).
- The MCP flag's name and the safety defaults.
- How the skills are generated, and how the plugin fetches the binaries for each platform.

## How to start

1. Stewart answers decisions 5 and 6.
2. Claude writes the ADRs for its own decisions, and adds the release exception
   (decision 7) to CLAUDE.md.
3. `task-planner` files Part 1 as tasks, pilot first (`curl-ftp`), the rest depending on it.
4. Part 2 is filed once the pilot sub-console works, MCP library first, then one server per
   sub-console; Part 3 once the first MCP server works.
5. The dark factory works the tasks; the gap office gains a per-sub-console reference
   measurement; Surl enables the plugin once its first release is out.
