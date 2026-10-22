# ADR-0731 — Protocol projects sit in a Protocols solution folder, one folder per protocol

- Status: Accepted
- Date: 2026-10-21
- Task: BL-2963
- Asked for by Stewart (2026-10-21); the details decided by Claude under Stewart's delegation.

## Context

`Curl.slnx` listed its 100 projects as one flat run with no solution folders around them, and 35
of them are `Curl.Protocol.*`. Stewart asked: "put all the Protocols and their tests in a solution
folder by the protocol name under one solution folder called Protocols such that we find a single
Protocol folder for each protocol under it."

## Decision

- `Curl.slnx` has a `/Protocols/` solution folder with one `/Protocols/<Name>/` folder for each
  `Curl.Protocol.<Name>.*` group. Each holds that protocol's library and its test projects, in
  the usual order: `.IntegrationTests`, `.UnitLibrary`, `.UnitTests`.
- `Curl.Protocol.Abstractions.*` is not a protocol, but it is the contract every protocol
  references, so it gets `/Protocols/Abstractions/`.
- Only projects named `Curl.Protocol.*` move. Libraries a protocol builds on but that are not
  named as protocols (`Curl.Tls`, `Curl.Quic`, `Curl.Http2`, `Curl.Http3` and the like) stay in the
  flat run.
- Solution folders exist only in the solution file. The project directories on disk stay flat at
  the repository root, so namespaces, project references, builds, tests and CI do not change.

## Consequences

- A new protocol is added with `dotnet sln Curl.slnx add --solution-folder Protocols/<Name> ...`
  (the `new-project` skill says so).
- `CLAUDE.md`'s rule that projects are "one flat run with no solution folders around them" now
  excepts the protocols.

## Amendment, 2026-10-21: conformance projects (BL-3007)

Stewart asked the same day: "Can we create a solution folder and put all the conformance projects
in it", and chose one subfolder per part, as for the protocols. `Curl.slnx` has a `/Conformance/`
folder with `/Conformance/<Part>/` for each `Curl.Conformance.<Part>.*` group (Http2Server,
Http3Server, LibTest, SshServer, UnitProgram), each with its library and tests. The harness itself,
`Curl.Conformance.UnitLibrary` and `Curl.Conformance.UnitTests`, has no part in its name, so it sits
in `/Conformance/Harness/`, named for what it is. As before, only the solution file changes.

## Amendment, 2026-10-21: shared projects (BL-3008)

Stewart asked the same day to do the same for the shared projects. `Curl.slnx` has a `/Shared/`
folder holding the five `.shproj` projects - Audit, Claude (`.claude`), Documentation, Gap and
Tasks - flat and in alphabetical order, since none has a test project beside it. Only the solution
file changes; the `Audit/` and `Gap/` folders themselves are untouched.

## Amendment, 2026-10-21: every area in a group folder (BL-3009)

Stewart asked the same day for groupings of the remaining flat projects ("There are too many projects
to organise myself when looking at a flat representation of the solution") and accepted these. Each
area sits with its tests in `/<Group>/<Area>/`, so no project stays in a flat run:

- `/Hosts/`: Api, Console, Libcurl, Mcp - the four ways Curl is used.
- `/CommandLine/`: Cli, Config, Output.
- `/Transfer/`: Cookies, Core, Engine, Networking.
- `/Wire/`: Http2, Http3, Quic, Zstandard - framing and encoding below the protocols.
- `/Security/`: Authentication, Cryptography, HttpMessageSignatures, Kerberos, Ntlm, Tls.

Tls went to Security rather than Wire because it is hand-built cryptography; Zstandard to Wire as
the nearest fit for an encoding; Cookies to Transfer, where the cookie engine runs. A new area goes in
the group it belongs to, in a folder named for the area. As before, only the solution file changes.
