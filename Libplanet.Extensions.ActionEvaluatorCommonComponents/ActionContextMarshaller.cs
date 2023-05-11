using System.Security.Cryptography;
using Bencodex;
using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Blocks;
using Libplanet.Tx;
using Boolean = Bencodex.Types.Boolean;

namespace Libplanet.Extensions.ActionEvaluatorCommonComponents;

public static class ActionContextMarshaller
{
    private static readonly Codec Codec = new Codec();

    public static byte[] Serialize(this IActionContext actionContext)
    {
        return Codec.Encode(Marshal(actionContext));
    }

    public static Dictionary Marshal(this IActionContext actionContext)
    {
        var dictionary = Bencodex.Types.Dictionary.Empty
            .Add("block_action", actionContext.BlockAction)
            .Add("miner", actionContext.Miner.ToHex())
            .Add("rehearsal", actionContext.Rehearsal)
            .Add("block_index", actionContext.BlockIndex)
            .Add("random_seed", actionContext.Random.Seed)
            .Add("signer", actionContext.Signer.ToHex())
            .Add("previous_states", AccountStateDeltaMarshaller.Marshal(actionContext.PreviousStates));

        if (actionContext.GenesisHash is { } genesisHash)
        {
            dictionary = dictionary.Add("genesis_hash", genesisHash.ByteArray);
        }

        if (actionContext.TxId is { } txId)
        {
            dictionary = dictionary.Add("tx_id", txId.ByteArray);
        }

        if (actionContext.PreviousStateRootHash is { } previousStateRootHash)
        {
            dictionary = dictionary.Add("previous_state_root_hash", previousStateRootHash.ByteArray);
        }

        return dictionary;
    }

    public static ActionContext Unmarshal(Dictionary dictionary)
    {
        return new ActionContext(
            genesisHash: dictionary.TryGetValue((Text)"genesis_hash", out IValue genesisHashValue) &&
                         genesisHashValue is Binary genesisHashBinaryValue
                ? new BlockHash(genesisHashBinaryValue.ByteArray)
                : null,
            blockIndex: (Integer)dictionary["block_index"],
            signer: new Address(((Text)dictionary["signer"]).Value),
            txId: dictionary.TryGetValue((Text)"tx_id", out IValue txIdValue) &&
                  txIdValue is Binary txIdBinaryValue
                ? new TxId(txIdBinaryValue.ByteArray)
                : null,
            blockAction: (Boolean)dictionary["block_action"],
            miner: new Address(((Text)dictionary["miner"]).Value),
            rehearsal: (Boolean)dictionary["rehearsal"],
            previousStateRootHash: dictionary.ContainsKey("previous_state_root_hash")
                ? new HashDigest<SHA256>(((Binary)dictionary["previous_state_root_hash"]).ByteArray)
                : null,
            previousStates: AccountStateDeltaMarshaller.Unmarshal(dictionary["previous_states"]),
            random: new Random((Integer)dictionary["random_seed"])
        );
    }

    public static ActionContext Deserialize(byte[] serialized)
    {
        var decoded = Codec.Decode(serialized);
        if (!(decoded is Dictionary dictionary))
        {
            throw new ArgumentException($"Expected 'Dictionary' but {decoded.GetType().Name}", nameof(serialized));
        }

        return Unmarshal(dictionary);
    }
}
