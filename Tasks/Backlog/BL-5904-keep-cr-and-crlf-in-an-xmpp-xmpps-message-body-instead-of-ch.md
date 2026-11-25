---
id: BL-5904
title: Keep CR and CRLF in an xmpp / xmpps message body instead of changing them to LF
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Xmpp.UnitLibrary/Client/XmppClientFlow.cs, Curl.Extended.Protocol.Xmpp.UnitTests, Documentation/Conformance/Extended/Xmpp.md]
requirement: none
created: 2026-11-24
completed:
---
# BL-5904 — Keep CR and CRLF in an xmpp / xmpps message body instead of changing them to LF

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

- [ ] A body holding `a`, CR, LF, `b`, CR, `c`, LF, `d` parses back from the sent stanza to exactly those characters, pinned by a unit test that gives the same bytes on Windows, Linux and macOS.
- [ ] A body holding a character XML 1.0 forbids still exits 43.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created.
