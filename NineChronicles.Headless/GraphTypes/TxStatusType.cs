using System.ComponentModel;
using GraphQL.Types;

namespace NineChronicles.Headless.GraphTypes
{
    [Description("The result of querying transaction.")]
    public enum TxStatus
    {
        [Description("The Transaction doesn't staged or invalid.")]
        INVALID,
        
        [Description("The Transaction do not executed yet.")]
        STAGING,
        
        [Description("The Transaction is success.")]
        SUCCESS,
        
        [Description("The Transaction is failure.")]
        FAILURE,
    }

    public class TxStatusType : EnumerationGraphType<TxStatus>
    {
        
    }
}
