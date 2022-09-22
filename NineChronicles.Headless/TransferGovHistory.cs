using Libplanet;
using Libplanet.Assets;
using Libplanet.Blocks;
using Libplanet.Tx;

namespace NineChronicles.Headless
{
    public class TransferGovHistory
    {
        public BlockHash BlockHash { get; }

        public TxId TxId { get; }

        public Address Sender { get; }

        public Address Recipient { get; }

        public FungibleAssetValue Amount { get; }

        public string? Memo { get; }

        public TransferGovHistory(
            BlockHash blockHash,
            TxId txId,
            Address sender,
            Address recipient,
            FungibleAssetValue amount,
            string? memo)
        {
            BlockHash = blockHash;
            TxId = txId;
            Sender = sender;
            Recipient = recipient;
            Amount = amount;
            Memo = memo;
        }
    }
}
