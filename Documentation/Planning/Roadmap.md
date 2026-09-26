# Roadmap

Milestones 0 and 1 are set; later phases are not yet sequenced here. Dates are
deliberately absent rather than guessed.

Ordered by dependency, not by calendar. A milestone is done when its exit
criteria are met, and each milestone cites the requirement IDs it delivers.

## Milestone 0 — Foundations

Repository, solution and documentation structure.

- **Status:** In progress
- **Delivers:** `Curl.slnx`, `Documentation` shared project, bootstrap scripts
- **Exit criteria:** solution opens cleanly in Visual Studio and builds from the
  `dotnet` CLI; documentation structure agreed
- **Done:** `.slnx` solution and `Documentation.shproj` created and verified
  (2026-09-25) — see `Decisions/ADR-0001-adopt-slnx-solution-format.md`
- **Done:** all sixteen Phase 1 projects exist and are listed in `Curl.slnx`;
  `dotnet build Curl.slnx -warnaserror` is clean with no `NU1503` (2026-09-26, BL-003)
- **Outstanding:** nothing.

## Milestone 1 — Phase 1, with the small Phase 4 protocols beside it

Phase 1 of `Documentation/Product/Product-Overview.md` (`Abstractions`, `Networking`,
`Core`, `Cli`, `Output`, `Console`, `File` and `Http`), with `Curl.Authentication` and
`Curl.Cookies` moved in from Phase 2 (ADR-0016), and DICT, Gopher, Telnet, TFTP and MQTT
built alongside it.

- **Status:** In progress
- **Decision (Stewart, 2026-09-26):** DICT, Gopher, Telnet, TFTP and MQTT are built in
  parallel with Phase 1, for throughput, because the dark factory runs parallel lanes.
  WS stays in Phase 4.
- **Decisions that stand:** ADR-0005 (protocol handlers acquire transports through
  connectors) and ADR-0006 (the transfer context carries the Phase 4 protocol options)
  remain Accepted, and `dict://` sends curl's exact `CLIENT libcurl <version>` line
  (`CLIENT libcurl 8.21.0`, FR-020).
- **Delivers:**
  - `Curl.Console` composition: the runner, composition root and `file://` (BL-068),
    the TCP and UDP connectors composed with the TLS provider (BL-069), the dict,
    gopher, telnet, tftp and mqtt handlers registered (BL-070), and the TLS options
    mapped onto the TLS provider (BL-071, BL-072).
  - The production TLS transport, in `Curl.Networking.UnitLibrary` behind
    `ITlsProvider` (ADR-0005; the Product Overview's "TLS lives once" finding): TLS
    failures as a `ConnectResult` (BL-061), `SslStreamTlsProvider` with `-k`, TLS
    versions and exits 35 and 60 (BL-062), `--cacert` (BL-063), failure messages and
    `--capath` (BL-064), `--cert`/`--key` (BL-065) and `--ciphers` (BL-066); their
    command-line parsing in `Curl.Cli.UnitLibrary` (BL-067); and Stewart's decisions on
    which curl TLS build to match (BL-059) and on `--ciphers` with the base class
    library (BL-060).
  - The protocols: `file://` (FR-001 to FR-019) and `dict`, `gopher`, `gophers`,
    `telnet`, `tftp`, `mqtt` and `mqtts` (FR-020 to FR-045).
  - `Curl.Authentication` and `Curl.Cookies`, moved from Phase 2 by ADR-0016 so that
    HTTP's `-u`, `--digest`, `-b` and `-c` work in Phase 1: Basic and Bearer, Digest and
    scheme choice (BL-216 to BL-218), and cookies (BL-219 to BL-223).
  - HTTP and the other Phase 1 option groups are part of Phase 1 but have no tasks yet.
- **Exit criteria:** `curl <url>` runs end to end through `Curl.Console` for `file`,
  `dict`, `gopher`, `gophers`, `telnet`, `tftp`, `mqtt` and `mqtts`, and returns curl's
  exit codes.
- **Done:** ADR-0005 and ADR-0006 (BL-032); requirements FR-020 to FR-045 (BL-033);
  contracts (BL-034, BL-035); scheme dispatch (BL-036); option parser (BL-037, BL-038);
  connectors (BL-039, BL-040); handlers for dict (BL-041), gopher (BL-042), telnet
  (BL-043, BL-044), tftp (BL-045, BL-046, BL-047) and mqtt (BL-048, BL-049); TLS
  (BL-061, BL-062) and its option parsing (BL-067); `curl <url>` end to end for
  `file://` (BL-068). All as of 2026-09-26.
- **Outstanding:** BL-063 to BL-066 and BL-069 to BL-072, with BL-059 and BL-060
  waiting on Stewart; HTTP is not yet planned.

## Later / unscheduled

Work that is agreed in principle but not yet sequenced.

- Hand-written HTTP/2 over `IConnection` (HPACK and framing; the BCL has none). Until
  then `--http2`, `--http2-prior-knowledge` and `--http3` are refused on every platform;
  see [ADR-0017](Decisions/ADR-0017-no-http-2-or-http-3-in-milestone-1.md).
- Hand-written zstd decoder (then `--compressed` advertises `zstd`). Until then
  `--compressed` sends `Accept-Encoding: deflate, gzip, br`; see
  [ADR-0020](Decisions/ADR-0020-compressed-advertises-deflate-gzip-and-br-until-a-zstd-decoder-exists.md).
