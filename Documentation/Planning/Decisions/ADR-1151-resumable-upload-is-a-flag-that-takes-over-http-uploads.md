# ADR-1151: --resumable-upload is a flag that takes over an HTTP -T upload

- Status: Accepted
- Date: 2026-10-30
- Task: BL-4129

Decided by Claude under Stewart's delegation.

## Context

BL-4129 adds the resumable upload extension to Curl (ADR-0999). Its wire format (ADR-1095) and its upload
flows (ADR-1103) landed under BL-4306 and BL-4307; what is left is the option that turns it on, its
registration and its description. Upstream curl has no such option, so there is no curl output to match.

## Decision

- **Option.** One flag, `--resumable-upload`, with no argument (`ExtensionOptionArgument.None`). It is an
  option extension, so it registers one line each in `ExtensionOptionCatalogue.cs`,
  `ExtensionTransferHookCatalogue.cs` and `ExtensionDescriptionCatalogue.cs`, as `--segments` does, and
  nothing in `ExtensionCatalogue.cs`, which holds scheme handlers. No scheme is added: the upload goes to
  the transfer's own `http://` or `https://` URL.
- **Activation.** `ResumableUploadExtension.HookFor` gives a `ResumableUploadTransferHook` only when the
  option group gave the flag, so without it every transfer is curl's. With it, only an http or https
  transfer with a `-T` body is taken over (ADR-1103); any other transfer still goes to curl's handler.
  With `CURL_EXTENSIONS=0` the option is not registered and is refused as an unknown option (exit 2).
- **Specification versions.** draft-ietf-httpbis-resumable-upload, latest draft, with
  `Upload-Draft-Interop-Version: 6`; tus 1.0.0 (Core Protocol and the Creation extension). tus is
  chosen when the server's `OPTIONS` response offers it, otherwise the IETF draft.
- **Resumptions.** The hook uses its default of 3 resumptions. No option sets it: one flag keeps the
  command line simple, and a broken connection that fails three resumptions is unlikely to succeed on a
  fourth.

## Left out, and why

- **A resumption count option** (see above); it can be added as its own value option if wanted.
- **tus extensions other than Creation** (Creation With Upload, Expiration, Checksum, Termination,
  Concatenation): none is needed to upload and resume one body.
- **The IETF draft's 104 interim `Location`**, and upload cancellation: ADR-1103 uses the final
  response's `Location`, and curl never cancels a transfer it can still finish.
- **Forward HTTP proxies**: requests use origin-form targets, so only a tunnelling proxy works (ADR-1103).

## Consequences

`curl --extensions` and `curl --ai-help extensions` list the extension. The extension is tested against the
two specifications, never against curl, and changes nothing curl does without the flag.
