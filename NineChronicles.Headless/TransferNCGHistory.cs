using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;

namespace NineChronicles.Headless
{
    public class TransferNCGHistory
    {
        public BlockHash BlockHash { get; }

        public TxId TxId { get; }

        public Address Sender { get; }

        public Address Recipient { get; }

        public FungibleAssetValue Amount { get; }

        public string? Memo { get; }

        public TransferNCGHistory(
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
