namespace Curl.Protocol.Tftp;

/// <summary>
/// One name and value pair read from an option acknowledgement, with what curl 8.21.0
/// took from it.
/// </summary>
/// <param name="Name">The option's name, as the server sent it.</param>
/// <param name="Value">The option's value, as the server sent it.</param>
/// <param name="BlockSize">
/// The block size this <c>blksize</c> option granted, or <see langword="null" /> when the
/// option is not <c>blksize</c> or was rejected.
/// </param>
/// <param name="TransferSize">
/// The download size this <c>tsize</c> option announced, or <see langword="null" /> when
/// the option is not <c>tsize</c>, the transfer is an upload, or the value was ignored or
/// rejected.
/// </param>
internal sealed record TftpAcknowledgedOption(string Name, string Value, int? BlockSize, long? TransferSize);
