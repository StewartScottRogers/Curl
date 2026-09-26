---
description: Bring one curl URL scheme from empty scaffold to a working, conformance-checked protocol handler.
argument-hint: <scheme, e.g. gopher>
---
Implement the **$ARGUMENTS** scheme in `Curl.Protocol.$ARGUMENTS.UnitLibrary`.

This is the `/feature` pipeline aimed at one protocol. Run `/feature`'s stages, with these
additions specific to a protocol:

1. Before planning, confirm the target library and its `.UnitTests` twin exist in
   `Curl.slnx`. If not, invoke `new-project`.

2. `protocol-architect` must additionally settle, and state in the plan:
   - the exact scheme strings for `IProtocolHandler.SupportedSchemes`, lowercased, and
     whether the secure variant is the same handler over an `ITlsProvider`-upgraded
     connection rather than a second handler;
   - the wire conversation as a byte script — what Curl sends, what the server sends back,
     in order, for the happy path and for each failure;
   - the `CurlExitCode` for every failure in that script, taken from
     https://curl.se/libcurl/c/libcurl-errors.html;
   - which of curl's options apply to this scheme, from https://curl.se/docs/manpage.html.

3. `test-writer` builds the fake `IConnection` from that byte script. No test in the
   protocol's `.UnitTests` project may carry `[TestCategory("Integration")]` — needing
   a live server means the seam is wrong, so report it instead of tagging around it.

4. `protocol-implementer` writes the handler. It references
   `Curl.Protocol.Abstractions.UnitLibrary` and nothing else horizontal. A need for another
   protocol's code is a stop-and-report, not a project reference.

5. `conformance-auditor` is mandatory here, not optional: a protocol is not done until its
   options, exit codes, and output bytes have been compared against upstream curl.

Report at the end: schemes registered, the DI registration added, test count, the exit codes
covered, and any option of curl's for this scheme that is still unimplemented — as tasks
filed by `task-planner`, not as a footnote.
