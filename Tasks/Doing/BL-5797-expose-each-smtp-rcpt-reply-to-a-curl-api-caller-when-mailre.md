---
id: BL-5797
title: Expose each SMTP RCPT reply to a Curl.Api caller when MailRecipientAllowFails lets a recipient fail
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api.UnitLibrary/Options/Protocols/EasySmtpOptions.cs, Curl.Api/Messages]
requirement: none
created: 2026-11-22
completed:
---
# BL-5797 — Expose each SMTP RCPT reply to a Curl.Api caller when MailRecipientAllowFails lets a recipient fail

## Goal

A `Curl.Api` caller sending mail with `MailRecipientAllowFails = true` can see which recipients the server refused and its reply to each, the information `curl -v --mail-rcpt-allowfails` prints.

## Context

From Surly cookbook bug report `2026-11-22T2157-Curl-bug-report.md` (batch 13 of the 1,000-recipe plan), finding 3. Build tested: Curl `2145a49eae`, Surl v0.1.5 `d64faa4bb`, Windows 11.

Repro: `surl --mail-domain example.com --mailbox ann@example.com:pw smtp://127.0.0.1:2525/`; a `Curl.Api` send to `ann@example.com` and `nobody@example.com` with `MailRecipientAllowFails = true` gets status 250 and nothing says `nobody` was refused with 550. curl shows it only in `-v`/`--trace` output (libcurl in its debug callback), and `Curl.Api` has no debug or trace callback. Decide (ADR, decided by Claude under Stewart's delegation) the simplest way that stays true to curl: either a general `Curl.Api` debug/trace callback mirroring `CURLOPT_DEBUGFUNCTION` (info, header in/out, data in/out), which also serves every other protocol, or a typed per-recipient result on the SMTP response. Prefer the general debug callback if `Curl.Api` lacks one, since it closes the gap for every "what did the server say" question; secrets must be redacted exactly as curl's `-v` redacts them. Update the `Curl.Api` README. Recipe `smtp-send-despite-a-bad-recipient-in-csharp`.

## Acceptance criteria

- [ ] An ADR records the choice and why.
- [ ] Against an SMTP test server that refuses one of two recipients, a `Curl.Api` caller with `MailRecipientAllowFails` observes the 550 reply for that recipient and the 250 for the other through the chosen API, pinned by a test.
- [ ] Nothing a curl `-v` would hide (credentials, `Authorization`) reaches the caller through it, pinned by a test; the README documents it.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-22: Created from Surly bug report 2026-11-22T2157, finding 3.
- 2026-11-22: Backlog -> Doing. Criteria at claim: 77b5bb79 ad55a03b 16f1db5b 3d836d5e.
