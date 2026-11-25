# ADR-1359: The OpenSSL build honours TRUSTED CERTIFICATE settings in --capath

- Status: Accepted
- Date: 2026-11-24
- Decided by Claude under Stewart's delegation
- Task: BL-5917 (follows ADR-1351)

## Context

ADR-1351 honoured `TRUSTED CERTIFICATE` blocks in `--cacert` only; a `--capath` hashed file
in that format added nothing. OpenSSL's `by_dir` reads those files too.

Measured with curl 8.18.0 (OpenSSL 3.5.5) on Ubuntu:

- A root trusted for `serverAuth` in a `--capath` file verifies (exit 0); one rejecting it,
  or a rejected leaf with nothing listed above it, is exit 60 `certificate rejected (28)`.
- Of two copies of one certificate the first wins: `<hash>.0` before `<hash>.1`, an earlier
  directory before a later one.
- A certificate in `--cacert` keeps that file's settings over the `--capath` copy's.
- A rejected `--capath` root beside an unrelated `--cacert` file is still exit 60 (28).
- A file whose first block cannot be decoded adds nothing.

## Decision

`OpenSslCaFile` reads `--capath` files as it reads `--cacert`, keeping the blocks before an
undecodable one, and `FollowedBy` gives the `--cacert` copy of a certificate precedence.
Without `--cacert`, the chain is rebuilt against the `--capath` certificates to find the one
that decides. A rejected certificate that is the root of a system store chain built without
fault is not refused: OpenSSL curl loads its default CA file first, so the file's plain copy
wins there as a `--cacert` copy does.

## Why

It is what the platform's curl does, with the parser ADR-1351 already added. The system
store stands for OpenSSL's default CA file, and a sound chain's root is the one certificate
known to be in it without reading the machine's store.
