---
id: BL-4733
title: Summarise every Original and Extended protocol on a README section and a Documentation/Protocols.md page
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [README.md, Documentation/Protocols.md, CLAUDE.md, DarkFactory/LaneRules.md]
lane: no
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4733 — Summarise every Original and Extended protocol on a README section and a Documentation/Protocols.md page

## Goal

GitHub's front page (README.md) shows Curl's Original and Extended protocols, and links to a sub page, Documentation/Protocols.md, that summarises every one of them.

## Context

Stewart, 2026-11-03: "update the readme.md file for github to reflect both the Original and Extended Protocols with a sub page that provides a summary of all of them" - approved as: a README Protocols section after "What Curl is"; a sub page with one table for Original (`Curl.Original.Protocol.*.UnitLibrary`, 16 libraries) and one for Extended (`Curl.Extended.Protocol.*.UnitLibrary`, 20 libraries); every row taken from the code; a rule so each new protocol adds its row. Option extensions (`Curl.Extension.*`, no URL scheme of their own) are out of scope.

## Acceptance criteria

- [x] README.md has a `## Protocols` section after `## What Curl is` listing the Original protocols' URL schemes and the Extended protocols' URL schemes, saying Extended ones are switched off by `CURL_EXTENSIONS=0`, and linking to `Documentation/Protocols.md`.
- [x] `Documentation/Protocols.md` has an Original table with one row per `Curl.Original.Protocol.*.UnitLibrary` folder (16) and an Extended table with one row per `Curl.Extended.Protocol.*.UnitLibrary` folder (20); each row gives the URL scheme(s) the handler accepts, one line on what it does, the specification it follows, a link to its library folder, and how it is tested.
- [x] Every scheme listed is one the library's handler actually accepts (checked against the handler code, not the folder name).
- [x] `CLAUDE.md` ("Extensions" and protocol naming) and `DarkFactory/LaneRules.md` both say a task adding a protocol library also adds its row to `Documentation/Protocols.md` and its scheme to README's Protocols section.
- [x] `dotnet build` is clean.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 112db8be ac8dafdb f02ca305 6b23aa00 42b24d12.
- 2026-11-03: Doing -> Done. README Protocols section and Documentation/Protocols.md written; schemes checked against each handler's SupportedSchemes; build clean.
