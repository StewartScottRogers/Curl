# ADR-0016 — `Curl.Authentication` and `Curl.Cookies` move into Milestone 1

- **Status:** Accepted
- **Date:** 2026-09-26

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions").

## Context

`Documentation/Product/Product-Overview.md`, "Phasing", placed `Authentication` and
`Cookies` in Phase 2, beside `Ftp` and `Ssh`. `Documentation/Planning/Roadmap.md`,
"Milestone 1", delivers Phase 1 (`Abstractions`, `Networking`, `Core`, `Cli`,
`Output`, `Console`, `File` and `Http`) and did not mention either library.

The Phase 1 HTTP plan (protocol-architect, 2026-09-26, item D1) needs a decision on
whether they wait:

- `curl http(s)://` with `-u`/`--user`, `--basic`, `--digest`, `--oauth2-bearer`,
  `-b`/`--cookie` and `-c`/`--cookie-jar` is everyday curl
  (<https://curl.se/docs/manpage.html>). An HTTP handler without them is not a drop-in
  replacement for HTTP, which is what Phase 1 claims to prove.
- ADR-0014 already defines the auth and cookie seams the HTTP handler calls, so the
  HTTP handler can be built against them whether or not the libraries are filled.
- `Curl.Authentication.UnitLibrary` and `Curl.Cookies.UnitLibrary` are empty today and
  share no project with `Curl.Protocol.Http.UnitLibrary`, so their tasks do not
  overlap the HTTP tasks' `touches` and can run in parallel dark factory lanes.

## Decision

`Curl.Authentication` and `Curl.Cookies` are built in Milestone 1, as part of Phase 1.
Phase 2 keeps `Ftp` and `Ssh`.

In Milestone 1 they deliver what HTTP needs in Phase 1: Basic and Bearer, Digest and
the choice between schemes (BL-216 to BL-218), and the cookie engine, jar and file
format (BL-219 to BL-223), with the handler seams (BL-181, BL-182) and the
`Curl.Console` composition (BL-237). The remaining schemes the Product Overview lists
for `Curl.Authentication` (NTLM, Negotiate, AWS SigV4) are not pulled forward by this
decision; they are planned when their tasks are filed.

## Consequences

Good:

- Phase 1's HTTP is usable for the requests curl users actually send, so the
  Milestone 1 exit claim means something for HTTP.
- Two more disjoint libraries give the dark factory more parallel work while the HTTP
  handler is built.

Costs:

- Milestone 1 grows by two libraries and their tests, each held to the 100% coverage
  gates, so it finishes later than it would have.
- Phase 2 now proves only the second transport shape (FTP's control and data
  channels, SSH); it no longer exercises service injection of auth and cookies for
  the first time, because Phase 1 does.

## Alternatives considered

- **Keep both in Phase 2.** Rejected: Phase 1 would ship an HTTP that fails on `-u`
  and `-b`, and the HTTP handler's auth and cookie paths would be untested against
  real implementations until Phase 2.
- **Implement auth and cookies inside `Curl.Protocol.Http.UnitLibrary` for now.**
  Rejected: FTP, IMAP, SMTP and others authenticate too, and the Product Overview
  gives both concerns their own libraries injected as services. Moving the code out
  later would be rework, and would serialise it behind the HTTP tasks.
- **Move only Authentication, or only Cookies.** Rejected: both are everyday HTTP
  options, both are empty and disjoint, and splitting them buys nothing.
