# ADR-0683 — A curl_formadd file named "-" reads the engine's standard input, and file parts are read once per buffer fill

- **Status:** Accepted
- **Date:** 2026-10-18

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2625.

## Context

Upstream case 650 (lib650) posts a `curl_formadd` form whose last field is `CURLFORM_FILE` `"-"`. curl 8.22.0's
`Curl_getformdata` hands such a file to `curl_mime_data_cb` with `fread` and `fseeko` over `stdin`, of unknown size,
so the whole body goes chunked, and its chunks show how libcurl reads a MIME body: each buffer fill runs across parts,
reading a part through its read function (a file or a read callback) at most once (`read_part_content`'s
`STOP_FILLING`), and passing a file whose known size has been read without reading it again. Curl's library handle
had no standard input (`EasyTransferHost.Library` reads nothing for `-`), so the form's build opened a file named `-`
and failed with `CURLE_READ_ERROR` before anything was sent; and BL-2624 read only read-callback parts once per fill.

## Decision

1. A `CURLOPT_HTTPPOST` part, or one file of a field's several files, named `-` is sent as an `EasyMimeCallback` of
   size -1 over the handle's `EasyTransferHost.StandardInput` (`MimePostForm.BodyOf`), whose seek callback seeks it
   (`CantSeek` when it cannot), so a 301 re-POST rewinds it as `fseeko` does.
2. `ManagedCurlEngine`'s internal constructor takes a `standardInput`, which every handle it creates reads; the
   conformance harness passes the case's `<stdin>`. With none, handles keep `EasyTransferHost.Library`'s empty input:
   a library handle does not take the process's standard input on its own, so an application is never blocked on a
   console it did not hand over; the part is then sent empty.
3. `MultipartBodySegments.AddFileStream` marks an opened file part (not an encoded one), and in a body that holds a
   read-callback part `ConcatenatedReadStream` reads each file at most once per fill too, passing a seekable file at
   its end in the same fill. A body with no read-callback part still reads one segment per read, so the curl tool's
   `-F` bodies, which always have a known length, read as before.

## Consequences

Case 650 passes and is on the ratchet. A body's bytes never change, only where a chunked body's chunks break.
Encoded file parts still read as plain segments; a later case that needs their fill pattern adds them.
