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
completed: 2026-11-02
---
# BL-4611 — AzureStorage: keep a block upload within 50,000 blocks

## Goal

curl's `az://` block upload never commits a blob from more than 50,000 blocks (the Put Block List limit, Blob service REST API 2025-01-05).

## Context

Found by BL-4523: the client stages fixed 8 MiB blocks (`AzureStorageExtension.DefaultBlockSize`) with no cap on their count, so an upload over 390 GiB would stage more than 50,000 blocks and fail at Put Block List. Decide in an ADR whether to grow the block size (a block may be up to 4000 MiB) or fail before the first Put Block with a clear error. The matrix row `AzureStorage:PutBlock-4` is `n/a` until this lands.

## Acceptance criteria

- [x] A unit test in `Curl.Extended.Protocol.AzureStorage.UnitTests` pins that an upload whose length needs more than 50,000 blocks at 8 MiB stays within 50,000 blocks, or fails before the first Put Block, as the ADR decides.
- [x] `AzureStorage:PutBlock-4` in `Documentation/Conformance/Extended/AzureStorage.md` maps to that test.
- [x] The library stays at 100% line and branch coverage and complexity at most 10 (`Measure-CodeQuality.ps1`); the fast tests are green.

## Notes

- Pipeline `feature`, done directly: the change is two methods in one class, so the architect stage was not needed.
- Decision (ADR-1213, decided by Claude under Stewart's delegation): when the upload's length is known (a seekable stream, as `-T file` gives), `AzureBlobOperations.BlockSizeFor` grows the block from 8 MiB to the smallest size that keeps the blob within 50,000 blocks. Past 50,000 blocks of `Array.MaxLength` (about 100 TiB) it fails with exit 63 before the first Put Block. When the length is unknown (stdin), it fails with exit 63 before the Put Block that would make a 50,001st block, and commits nothing. Growing the block keeps every upload the service accepts working instead of failing large files.
- The block limit is a constructor parameter of `AzureBlobOperations` and `AzureStorageProtocolHandler` (the extension passes `AzureBlobOperations.MaxBlockCount`), so the handler tests pin both paths with a limit of 2.
- `AzureStorage:PutBlock-4` stays an `n/a:` row whose reason names the unit test. `RequirementMatrixTests` accepts only a `[SpecRequirement]` case in `Curl.Conformance.Extended.UnitTests`, and no fast case there can send 390 GiB. With the row as it is, `RequirementMatrixTests` passes.
- Measure-CodeQuality for Curl.Extended.Protocol.AzureStorage.UnitLibrary: 100% line, 100% branch, max complexity 10, 0 failing members. The fast tests are green (AzureStorage.UnitTests: 173 passed). The change is platform-neutral (no paths, OS text or certificates), which covers CI on Linux and macOS.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 3da8d675 a3a71d08 d5034162.
- 2026-11-02: Doing -> Done. An az:// block upload never commits more than 50,000 blocks: the block grows when the length is known, else exit 63 (ADR-1213)
