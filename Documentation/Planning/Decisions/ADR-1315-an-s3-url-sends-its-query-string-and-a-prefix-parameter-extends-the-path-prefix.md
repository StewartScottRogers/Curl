# ADR-1315: An s3:// URL sends its query string, and a prefix parameter extends the path prefix

Status: Accepted. Date: 2026-11-20. Task: BL-5607.

Decided by Claude under Stewart's delegation.

## Context

`curl s3://bucket/?prefix=logs/` dropped the query string, so a list could be narrowed only through the path.

## Decision

- The URL's query is split on `&`, percent-decoded (`+` is a space, as S3 reads it) and sent, percent-encoded again and sorted into the SigV4 canonical query string by the signer, with every request.
- GetObject sends the parameters as they are (`versionId`, `response-content-type`).
- ListObjectsV2: curl keeps `list-type=2` and its own `continuation-token`. A `delimiter` parameter replaces the default `/` (an empty one lists flat). A `prefix` parameter is appended to the path's prefix, so `s3://bucket/dir/?prefix=a` lists `dir/a*`; every other parameter (`max-keys`, `start-after`, `fetch-owner`) is sent unchanged.
- A `max-keys` below the number of keys still follows continuation tokens to the last page, as before.

## Why

Appending keeps the path prefix meaningful (it is what `s3://bucket/dir/` already means) and lets `?prefix=` narrow it, the one reading under which both parts count.
