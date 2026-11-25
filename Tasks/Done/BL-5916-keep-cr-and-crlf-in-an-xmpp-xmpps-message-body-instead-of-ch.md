---
id: BL-5916
title: Keep CR and CRLF in an xmpp / xmpps message body instead of changing them to LF
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Xmpp.UnitLibrary/Client/XmppClientFlow.cs, Curl.Extended.Protocol.Xmpp.UnitTests, Documentation/Conformance/Extended/Xmpp.md]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5916 — Keep CR and CRLF in an xmpp / xmpps message body instead of changing them to LF

## Goal

A CR or CR LF in an `xmpp://` message body reaches the server's XML parser unchanged.

## Context

Seen by reading the code during BL-5899, not yet measured: start with a failing test. `XmppClientFlow.Encode` serialises
the stanza with `XElement.ToString(SaveOptions.DisableFormatting)`, whose writer settings default to
`NewLineHandling.Replace`: a CR or CR LF in text is written as the platform's newline, never as `&#xD;`, and XML
line-end normalisation then gives the receiver a single LF. The body from `-d` or `-T` therefore changes with exit 0,
and a bare LF is written as CR LF on Windows only. `Documentation/Conformance/Extended/Xmpp.md` says the file's bytes
become the body. An `XmlWriter` with `NewLineHandling.Entitize` writes CR as `&#xD;` and leaves LF alone.

## Acceptance criteria

- [x] A body holding `a`, CR, LF, `b`, CR, `c`, LF, `d` parses back from the sent stanza to exactly those characters, pinned by a unit test that gives the same bytes on Windows, Linux and macOS.
- [x] A body holding a character XML 1.0 forbids still exits 43.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Reproduced first: `XmppUploadTests.ExecuteAsync_BodyHoldingCrAndLf_ParsesBackFromTheSentStanzaUnchanged` (`-d` and
  `-T`) failed on Windows, the stanza holding `a\r\nb\r\nc\r\nd`.
- Fix: `XmppClientFlow.Encode` writes through an `XmlWriter` with `NewLineHandling.Entitize`: CR goes out as `&#xD;`,
  LF in text as itself, on every platform. It covers every stanza sent, so a `subject` or `body` from the URL query too.
  No ADR: the only way to carry CR through XML line-end normalisation is the character reference the Context names.
- The test pins the exact stanza, which holds no platform newline, so it gives the same bytes on all three platforms.
- Forbidden character: `XmppProtocolHandlerTests` (body U+0001) still passes with exit 43.
- 2026-11-24: build 0 warnings, fast tests green (Xmpp 82 passed); `Measure-CodeQuality.ps1 -Library
  Curl.Extended.Protocol.Xmpp.UnitLibrary`: 0 failing members.
- Filed BL-5925: the disco answer written to the output still uses the platform's newline.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 5049e5b3 9d0eb0c1 3d836d5e.
- 2026-11-24: Doing -> Done. An xmpp message body keeps its CR, CR LF and LF on every platform
