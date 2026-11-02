---
id: BL-4495
title: Conformance: XMPP RFC 6120 stream, STARTTLS and SASL and RFC 6121 for xmpp://
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4462]
touches: [Curl.Conformance.Extended.UnitLibrary/Xmpp, Curl.Conformance.Extended.UnitTests/Xmpp, Documentation/Conformance/Extended/Xmpp.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4495 — Conformance: XMPP RFC 6120 stream, STARTTLS and SASL and RFC 6121 for xmpp://

## Goal

Curl's `xmpp://` and `xmpps://` client is measured against RFC 6120 (streams, STARTTLS, SASL, resource binding,
stanzas, errors) and the parts of RFC 6121 it uses through the real curl command line, every client requirement is
in a matrix mapped to a test, and every failure is fixed or listed with its fix task.

## Context

Specifications: RFC 6120 (4 streams: opening, `to`/`from`/`version`/`id`, closing, stream errors (4.9); 5 STARTTLS;
6 SASL with PLAIN (RFC 4616), ANONYMOUS (RFC 4505), SCRAM-SHA-1 and SCRAM-SHA-256 (RFC 5802, RFC 7677) and the
additional data with `<success/>`; 7 resource binding; 8 stanzas and stanza errors; 11 XML usage restrictions),
RFC 6121 (message stanzas as the client sends them), and XEP-0030 (service discovery) for the disco query ADR-1068
names. Every MUST and SHOULD that binds a client in those parts gets an ID; server-only ones are `n/a`.

Library: `Curl.Extended.Protocol.Xmpp.UnitLibrary` (no `CLAUDE.md`; `Client/XmppClientFlow`, `XmppRequests`, the
`XmppSasl*` and `XmppScram*` mechanisms, `Wire/XmppStreamReader`; ADR-1068: logs in to the URL's domain and sends a
message or a disco query to the path JID).

Part of the extended-protocol conformance plan Stewart approved on 2026-11-02. Follow
`Documentation/Conformance/Extended/README.md` and the ADR BL-4462 records: the harness, `[SpecRequirement]` IDs
(`Xmpp:RFC6120-<section>-<n>` and the like), the matrix format and the `KnownFailures.txt` rule. Measure against the
specification, never against upstream curl. The reference server implements XMPP and SCRAM from the RFCs itself (BCL
XML reader and cryptography only) and references no `Curl.Extended.Protocol.*` project. One file per case group;
nothing added to `Harness/`. This task touches only its own conformance files so protocols run in parallel: a fault in
its own server or cases is fixed here, but a defect in the extension library is listed in `KnownFailures.txt` with a
filed High task `Fix Xmpp conformance: <requirement>` (pipeline feature; touches the library, its `.UnitTests` and
`KnownFailures.txt`; criteria: the case passes, a focused unit test in the library's `.UnitTests` pins the fix
(ADR-0482), the `KnownFailures.txt` line is removed).

## Acceptance criteria

- [ ] `Documentation/Conformance/Extended/Xmpp.md` gives every MUST, MUST NOT, SHOULD and SHOULD NOT of this scope that
      binds a client an `Xmpp:` ID mapped to a test or to `n/a: <reason>`; the harness's matrix check passes.
- [ ] Cases in `Curl.Conformance.Extended.UnitTests/Xmpp/` run curl in process for `xmpp://` and `xmpps://` and assert
      stream bytes, stdout, stderr and exit code for at least: the stream header, STARTTLS offered as required and as
      optional, the stream restarted after TLS and after SASL, each SASL mechanism with the server's preference order,
      SCRAM with a bad server signature, `<failure/>` with each condition, resource binding with a server-assigned
      resource, a message stanza, a disco query and its result, a stanza error, a stream error, and a stanza split
      across reads.
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix Xmpp conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [ ] The code added under `Curl.Conformance.Extended.UnitLibrary/Xmpp/` is at 100% line and branch coverage,
      complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths, certificates or text).

## Notes

## Log

- 2026-11-02: Created.
