namespace Curl.Output;

/// <summary>
/// The C runtime whose <c>strftime</c> a curl build hands <c>-w</c> <c>%time{format}</c> to,
/// which decides what the format prints (ADR-0038, ADR-0076).
/// </summary>
public enum WriteOutTimeDialect
{
    /// <summary>The Microsoft C runtime of the Windows curl (mingw, Schannel).</summary>
    WindowsCRuntime,

    /// <summary>glibc in the C locale, as the Linux curl (OpenSSL) prints it.</summary>
    Glibc,
}
