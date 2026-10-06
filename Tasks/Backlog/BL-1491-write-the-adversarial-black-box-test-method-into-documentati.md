---
id: BL-1491
title: Write the adversarial black-box test method into Documentation/Wiki/Adversarial-Testing.md
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Wiki]
requirement: none
created: 2026-10-06
completed:
---
# BL-1491 — Write the adversarial black-box test method into Documentation/Wiki/Adversarial-Testing.md

## Goal

`Documentation/Wiki/Adversarial-Testing.md` states the method every per-project adversarial black-box test task follows, so 33 tasks attack their libraries the same way and to the same rules.

## Context

- Stewart's request, 2026-10-06: apply aggressive black-box testing (boundaries, semantic fuzzing and malformed input, equivalence-partition attacks on the invalid zones, state and concurrency stress) to every test project, after every other open task that changes that project's tests is done. This task writes the method; one task per `*.UnitTests` project applies it.
- Black box in Curl means the public surface of a library and, wherever behaviour is visible on the command line, real curl as the oracle: the expected answer is measured with `Record-CurlExchange.ps1` (the Schannel build on Windows, OpenSSL on Linux and macOS), never guessed from "implied" behaviour.
- The translation of the generic attack list to curl: SQL and XSS hardly apply; CRLF header injection, URL-parser confusion, path traversal through `-O` and `--output-dir`, config-file parsing, and malformed protocol and crypto records do.

## Acceptance criteria

- [ ] `Documentation/Wiki/Adversarial-Testing.md` exists and has sections for each of the four attack families (boundaries, malformed input, invalid partitions, state and concurrency), each with curl-specific examples.
- [ ] It states the rules: public surface only, injected fakes (`IConnection`, `TimeProvider`) and never a real network, MSTest and the base class library only with hand-written generators, every test passes on Windows, Linux and macOS, inputs over 1 MiB go in `TestCategory("Integration")` or are streamed, test names say what they attack, and a one-line comment says why only where the name cannot.
- [ ] It states what happens to a break: a red test is never committed; the defect is filed as a follow-up task (High when it is a crash, hang, memory blow-up or security issue) and its test lands with the fix.
- [ ] It points to the audit office's `audit-security` fuzzing and `audit-conformance` comparisons and says the per-project tasks add permanent tests rather than repeat those audits.
- [ ] `Documentation/Wiki/Home.md` links the page, and `Documentation/Wiki/Glossary.md` defines "adversarial black-box test".

## Notes

## Log

- 2026-10-06: Created.
