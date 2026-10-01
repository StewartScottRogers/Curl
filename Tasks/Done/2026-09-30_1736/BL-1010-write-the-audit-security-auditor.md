---
id: BL-1010
title: Write the audit-security auditor
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1001, BL-1005]
touches: [.claude/agents/audit-security.md, Audit/Instructions/Security.md]
lane: no
requirement: none
created: 2026-09-29
completed: 2026-09-30
---
# BL-1010 — Write the audit-security auditor

## Goal

A read-only `audit-security` agent audits Curl's hand-built cryptography, TLS, SSH and QUIC code - fuzzing its parsers, looking for timing leaks, and checking that secrets never reach logs or verbose output that real curl would not print - and reports in the audit report format.

## Context

Interactive only (`lane: no`): it writes `.claude/agents/audit-*` and `Audit/`. Run it
with `/task-run BL-1010`. Design: the ADR from BL-994 (security runs on Opus). Rules:
`Audit/Instructions/Auditor-Rules.md`; format: `Audit/Instructions/Report-Format.md`
(both BL-1001).

Two files:

- `.claude/agents/audit-security.md` - `name: audit-security`, a `description`,
  `tools: Read, Grep, Glob, Bash`, `model: opus`; body points to the two instruction
  files as in BL-1009.
- `Audit/Instructions/Security.md` - the method:
  1. Fuzz: run `Audit/Tools/Fuzz/Fuzz.cs` (BL-1005) for each target (`tls-handshake`,
     `cli`, `ssh`) with `--iterations` from the prompt (default 200000) and a fresh
     seed recorded in the report. Each saved crash or hang is a finding; its `--replay`
     command is the reproduction.
  2. Timing leaks: in `Curl.Cryptography.UnitLibrary`, `Curl.Tls.UnitLibrary`,
     `Curl.Protocol.Ssh.UnitLibrary`, `Curl.Quic.UnitLibrary`, `Curl.Kerberos.UnitLibrary`
     and `Curl.Ntlm.UnitLibrary`, every comparison of a MAC, tag, signature, password
     hash or other secret must use `CryptographicOperations.FixedTimeEquals`; grep for
     `SequenceEqual`, `==` on spans or arrays, and early-exit loops over such values.
     Secret-dependent branches or table lookups in hand-built ciphers (the
     Curve25519/Ed25519/ChaCha20-Poly1305 code CLAUDE.md says is hand-built) are
     findings with the line as evidence.
  3. Secrets in output: run Curl.Console with `-v` and with `--trace-ascii -` against
     `Record-CurlExchange.ps1`'s loopback server using `-u user:s3cr3t-probe`,
     `--proxy-user`, `-H "Authorization: Bearer s3cr3t-probe"` and an SSH key
     passphrase where the scheme allows; run real curl the same way. Any place Curl
     prints `s3cr3t-probe` (or its base64) where real curl does not is a finding. State
     the reference curl version. Also grep the code for secrets written to trace or log
     calls.
  4. Hostile-server input already covered by tests is not re-reported; a parser path
     with no negative test is Low unless the fuzzer found something.
  Severity: memory-unsafe or unbounded behaviour on hostile input, or a secret printed
  where curl does not, is Critical; a non-constant-time secret comparison is High.

## Acceptance criteria

- [x] `.claude/agents/audit-security.md` exists with `name: audit-security`, `model: opus`, `tools: Read, Grep, Glob, Bash` and no editing tool.
- [x] `Audit/Instructions/Security.md` states the four steps, names the six libraries, the fuzz targets, the probe secret and the severity rules above.
- [x] `claude agents` lists `audit-security`.
- [x] A trial run limited to step 2 on `Curl.Cryptography.UnitLibrary` in a detached worktree ends with one report block that parses with `ConvertFrom-Json`; command and summary recorded under Notes; the worktree is unchanged.

## Notes

- On the audit branch (worktree Z:/repos/Curl.auditbranch), commits a8778c4e and 545ca0b2, pull request https://github.com/StewartScottRogers/Curl/pull/38.
- claude agents: as for BL-1009, in Claude Code 2.1.284 it lists running sessions, not agent definitions; the trial's claude -p --agent audit-security is the proof the agent is found.
- Trial, limited to step 2 on Curl.Cryptography.UnitLibrary as the criteria say: a detached worktree of a8778c4e (Z:/repos/Curl.audit/trial-security, removed after), fingerprint a79416f2c8228e31056dcab01f60a6f2d56320f8414ba12413dd15ab95dd0a3a; claude -p --agent audit-security --dangerously-skip-permissions "Audit the tree at ... This is a limited trial: do step 2 ... only, and only in Curl.Cryptography.UnitLibrary ...". The worktree showed no changes afterwards.
- Report: one json block, parses with ConvertFrom-Json, all six fields, auditor security, the given commit and fingerprint, fuzz metrics null (step 1 skipped). Six High findings, all secret-indexed table lookups in hand-built ciphers: BlowfishState.cs:133 (also under BcryptPbkdf on an SSH key passphrase), Des.cs:170 (NTLM, password-derived keys), Cast128.cs:242 (SSH cast128-cbc), Camellia.cs:304 and Aria.cs:201 (TLS), Rc4.cs:113 (SSH arcfour, Kerberos rc4-hmac). Each annotated in phase 2 with the ADR that accepts it (ADR-0118, 0145, 0147, 0156), severity kept per rule 2 - Stewart's call at triage. Every MAC, tag, signature and key-check comparison it examined uses FixedTimeEquals or masked selection.
- The trial showed a gap: no key kind for a table lookup, so it used secret-dependent-branch. Added secret-dependent-lookup to Security.md (545ca0b2).

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. The read-only audit-security auditor fuzzes, checks constant-time comparisons and secret output, and reports in the audit format; in PR #38, awaiting Stewart's merge.
