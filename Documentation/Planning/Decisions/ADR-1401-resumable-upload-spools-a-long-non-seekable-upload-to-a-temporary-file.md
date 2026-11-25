# ADR-1401: Resumable upload spools a long non-seekable upload to a temporary file

- Status: Accepted
- Date: 2026-11-25
- Decided by Claude under Stewart's delegation (BL-5967)

## Context

Both tus 1.0 and the IETF draft announce the upload's length on creation, and a resumption sends again from the
server's offset, so a resumable upload needs content it can measure and reread. A seekable `-T` file gives both.
`-T -` and other non-seekable sources do not: the hook copied them whole into a `MemoryStream`, which holds at most
2 GiB and then throws an unhandled `IOException`.

Real curl has no resumable upload of this kind, so there is no behaviour of its to match.

## Decision

A non-seekable upload is uploaded, whatever its length.

- Its first 16 MiB (`ResumableUploadTransferHook.DefaultMemoryLimit`) are held in memory. A longer one is copied
  into a file in the temporary directory (`Path.GetTempFileName`, readable by its owner only on Linux and macOS),
  opened with `FileOptions.DeleteOnClose` and deleted when the transfer ends.
- A source that cannot be read ends with exit 26 (`CURLE_READ_ERROR`), "Failed reading the upload: ...".
- A temporary file that cannot be created or written, as on a full disk, ends with exit 23
  (`CURLE_WRITE_ERROR`), "Failed writing the upload to a temporary file: ...".
- Either failure happens before any request is sent.
- A seekable upload already read part way is not copied: it is sent from where it stands through a window over it.

## Alternatives

- **Refuse past the buffer.** Simpler, but large piped uploads, such as an archive made on the fly, are what a
  resumable upload is for.
- **Always spool to a file.** It writes small piped uploads, which may be secrets, to disk for no gain.
- **Keep only a sliding window in memory.** The server may report any offset, and neither the draft's nor tus's
  creation can be sent here without the length.

## Consequences

- A piped upload needs free temporary space equal to its length, and the whole source is read before the first
  request.
- A process killed during the transfer on Linux or macOS can leave the temporary file behind.
