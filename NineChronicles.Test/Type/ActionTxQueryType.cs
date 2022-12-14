using System.Security.Cryptography.X509Certificates;
using Libplanet;

namespace NineChronicles.Test.Type;

public class ActionTxQueryResponseType
{
    public ActionTxQueryType ActionTxQuery { get; set; }
}

public class ActionTxQueryType
{
    public string ActivateAccount  { get; set; }
}
