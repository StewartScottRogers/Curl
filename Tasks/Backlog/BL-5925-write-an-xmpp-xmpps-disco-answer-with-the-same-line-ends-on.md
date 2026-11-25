---
id: BL-5925
title: Write an xmpp / xmpps disco answer with the same line ends on every platform
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Xmpp.UnitLibrary/Client/XmppClientFlow.cs, Curl.Extended.Protocol.Xmpp.UnitTests, Documentation/Conformance/Extended/Xmpp.md]
requirement: none
created: 2026-11-24
completed:
---
# BL-5925 — Write an xmpp / xmpps disco answer with the same line ends on every platform

## Goal

The output of an `xmpp://` disco query holds the same line ends on Windows, Linux and macOS.

## Context

Seen by reading the code during BL-5916, not yet measured: start with a failing test.
`XmppClientFlow.WriteDiscoAnswerAsync` writes each payload element with `XElement.ToString(SaveOptions.DisableFormatting)`,
whose writer settings default to `NewLineHandling.Replace`: a line end in the server's answer (whitespace between
elements, or text) is written as the platform's newline, so CR LF on Windows and LF elsewhere. BL-5916 fixed the same
default for the stanzas sent. This is an extended protocol, so decide the line ends from the XML rules, not from curl.

## Acceptance criteria

- [ ] A disco answer whose payload holds a line end between elements and `&#xD;` in text is written as the same bytes on every platform, pinned by a unit test.
- [ ] `Documentation/Conformance/Extended/Xmpp.md` says which line ends the output holds.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created.
