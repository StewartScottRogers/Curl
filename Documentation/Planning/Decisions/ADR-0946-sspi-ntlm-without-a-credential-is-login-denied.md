# ADR-0946: SSPI NTLM that cannot acquire the credential for Type 1 is exit 67, Login denied

- Status: Accepted
- Date: 2026-10-28
- Task: BL-3649 (CGR-120)
- Decided by Claude under Stewart's delegation.

## Context

On Windows, curl's SSPI build makes the NTLM Type 1 message with
`AcquireCredentialsHandle` and fails the transfer with `CURLE_LOGIN_DENIED` (exit 67,
`Login denied`) when that call fails, before sending any request. SSPI refuses an explicit
user name that is too long (measured: 108 characters is accepted, 258 and up refused), so
`-u testuser<1100 x A>:testpass --ntlm` exits 67 with nothing on the wire. The BCL's
`NegotiateAuthentication` throws `Win32Exception` for the same failure, which
`SystemSecurityContext` reports as `SecurityContextStatus.NoCredentials`, and
`NtlmHttpAuthenticator` sent the request with no `Authorization` header and exited 0.

## Decision

Where the SSPI build is matched (`matchesSspiBuild`), a Type 1 step that answers
`NoCredentials` throws `HttpAuthenticationFailedException` with exit 67 and `Login denied`;
the HTTP handler fails the first request with it before sending a byte. Curl's own NTLM
(off Windows) still sends nothing, as the OpenSSL build sends a Type 1 for any user name.

Measured with curl 8.18.0 (mingw, SSPI) on Windows, the only SSPI build on the lane machine;
the report measured the same answer with curl 8.22.0 Schannel.

## Consequences

Any other failure of the Type 1 step still sends no header, as before.
