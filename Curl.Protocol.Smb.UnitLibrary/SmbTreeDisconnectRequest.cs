namespace Curl.Protocol.Smb;

/// <summary>
/// The SMB_COM_TREE_DISCONNECT request curl 8.21.0 ends a transfer with once the share is
/// connected (<c>smb_send_tree_disconnect</c>): no parameter words and no bytes.
/// </summary>
internal static class SmbTreeDisconnectRequest
{
    // Word count 0, byte count 0.
    private static ReadOnlySpan<byte> Body => [0x00, 0x00, 0x00];

    /// <summary>Encodes the request, NetBIOS header first.</summary>
    /// <param name="userId">The UID the session setup response assigned.</param>
    /// <param name="treeId">The TID the tree connect response assigned.</param>
    /// <returns>The bytes to send.</returns>
    public static byte[] Encode(ushort userId, ushort treeId) =>
        SmbMessageHeader.Frame(SmbMessageHeader.TreeDisconnectCommand, userId, treeId, Body);
}
