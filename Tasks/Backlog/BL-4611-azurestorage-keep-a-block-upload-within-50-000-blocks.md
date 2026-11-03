---
id: BL-4611
title: AzureStorage: keep a block upload within 50,000 blocks
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.AzureStorage.UnitLibrary/Client, Curl.Extended.Protocol.AzureStorage.UnitTests, Documentation/Conformance/Extended/AzureStorage.md]
requirement: none
created: 2026-11-02
completed:
---
# BL-4611 — AzureStorage: keep a block upload within 50,000 blocks

## Goal

curl's `az://` block upload never commits a blob from more than 50,000 blocks (the Put Block List limit, Blob service REST API 2025-01-05).

## Context

Found by BL-4523: the client stages fixed 8 MiB blocks (`AzureStorageExtension.DefaultBlockSize`) with no cap on their count, so an upload over 390 GiB would stage more than 50,000 blocks and fail at Put Block List. Decide in an ADR whether to grow the block size (a block may be up to 4000 MiB) or fail before the first Put Block with a clear error. The matrix row `AzureStorage:PutBlock-4` is `n/a` until this lands.

## Acceptance criteria

- [ ] A unit test in `Curl.Extended.Protocol.AzureStorage.UnitTests` pins that an upload whose length needs more than 50,000 blocks at 8 MiB stays within 50,000 blocks, or fails before the first Put Block, as the ADR decides.
- [ ] `AzureStorage:PutBlock-4` in `Documentation/Conformance/Extended/AzureStorage.md` maps to that test.
- [ ] The library stays at 100% line and branch coverage and complexity at most 10 (`Measure-CodeQuality.ps1`); the fast tests are green.

## Notes

## Log

- 2026-11-02: Created.
