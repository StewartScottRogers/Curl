namespace Curl.Extension.Protocol.AzureStorage.Bodies;

/// <summary>Which block list Put Block List searches for a block ID, named by the element that holds it.</summary>
public enum AzureBlockListSource
{
    /// <summary><c>Latest</c>: the uncommitted block if there is one, else the committed block.</summary>
    Latest,

    /// <summary><c>Committed</c>: the committed block only.</summary>
    Committed,

    /// <summary><c>Uncommitted</c>: the uncommitted block only.</summary>
    Uncommitted,
}
