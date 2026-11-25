---
id: BL-5928
title: Write an xmpp / xmpps disco answer with the same line ends on every platform
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Xmpp.UnitLibrary/Client/XmppClientFlow.cs, Curl.Extended.Protocol.Xmpp.UnitTests, Documentation/Conformance/Extended/Xmpp.md]
requirement: none
created: 2026-11-24
completed: 2026-11-25
---
# BL-5928 — Write an xmpp / xmpps disco answer with the same line ends on every platform

## Goal

The output of an `xmpp://` disco query holds the same line ends on Windows, Linux and macOS.

## Context

Seen by reading the code during BL-5916, not yet measured: start with a failing test.
`XmppClientFlow.WriteDiscoAnswerAsync` writes each payload element with `XElement.ToString(SaveOptions.DisableFormatting)`,
whose writer settings default to `NewLineHandling.Replace`: a line end in the server's answer (whitespace between
elements, or text) is written as the platform's newline, so CR LF on Windows and LF elsewhere. BL-5916 fixed the same
default for the stanzas sent. This is an extended protocol, so decide the line ends from the XML rules, not from curl.

## Acceptance criteria

- [x] A disco answer whose payload holds a line end between elements and `&#xD;` in text is written as the same bytes on every platform, pinned by a unit test.
- [x] `Documentation/Conformance/Extended/Xmpp.md` says which line ends the output holds.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- A failing test showed it first: `DisableFormatting` wrote each LF in text as CR LF on Windows, and a `&#xD;` as a newline.
- Serialised with the same `NewLineHandling.Entitize` settings as the stanzas sent: LF stays LF, CR is `&#xD;` (ADR-1378).
- CI on Linux and macOS: the output no longer depends on the platform; fast tests green on Windows, quality run 0 failing members.

## Log

- 2026-11-24: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: de46569f ce8c1752 3d836d5e.
- 2026-11-25: Doing -> Done. Disco answers are written with the same line ends on every platform
