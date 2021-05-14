using Libplanet;
using Libplanet.Assets;

namespace NineChronicles.Headless
{
    public class TransferNCGHistory
    {
        public Address Sender { get; }
        
        public Address Recipient { get; }

        public FungibleAssetValue Amount { get; }

        public TransferNCGHistory(Address sender, Address recipient, FungibleAssetValue amount)
        {
            Sender = sender;
            Recipient = recipient;
            Amount = amount;
        }
    }
}
