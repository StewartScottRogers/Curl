namespace Curl.Protocol.Smtp;

/// <summary>
/// What the open session tells <see cref="SmtpMailTransaction" /> about the parameters it may
/// add to <c>MAIL FROM</c> (BL-544).
/// </summary>
/// <param name="Authenticated">An <c>AUTH</c> exchange succeeded, so <c>AUTH=</c> is sent for <c>--mail-auth</c>.</param>
/// <param name="SizeAdvertised">The <c>EHLO</c> reply advertised <c>SIZE</c>.</param>
/// <param name="SmtpUtf8Advertised">The <c>EHLO</c> reply advertised <c>SMTPUTF8</c>.</param>
internal readonly record struct SmtpMailExtensions(bool Authenticated, bool SizeAdvertised, bool SmtpUtf8Advertised);
