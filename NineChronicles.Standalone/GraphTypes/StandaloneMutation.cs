using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Assets;
using Libplanet.Blockchain;
using Libplanet.Crypto;
using Nekoyume.Action;
using Nekoyume.Model.State;
using Serilog;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Standalone.GraphTypes
{
    public class StandaloneMutation : ObjectGraphType
    {
        private StandaloneContext StandaloneContext { get; }

        public StandaloneMutation(StandaloneContext standaloneContext)
        {
            StandaloneContext = standaloneContext;

            Field<KeyStoreMutation>(
                name: "keyStore",
                resolve: context => standaloneContext.KeyStore);

            Field<ActivationStatusMutation>(
                name: "activationStatus",
                resolve: context => standaloneContext.NineChroniclesNodeService);

            Field<ActionMutation>(
                name: "action",
                resolve: context => standaloneContext.NineChroniclesNodeService);

            Field<NonNullGraphType<BooleanGraphType>>(
                name: "transferGold",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "recipient",
                    },
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Name = "amount"
                    }
                ),
                resolve: context =>
                {
                    NineChroniclesNodeService service = standaloneContext.NineChroniclesNodeService;
                    PrivateKey privateKey = service.PrivateKey;
                    if (privateKey is null)
                    {
                        // FIXME We should cover this case on unittest.
                        var msg = "No private key was loaded.";
                        context.Errors.Add(new ExecutionError(msg));
                        Log.Error(msg);
                        return false;
                    }

                    BlockChain<NCAction> blockChain = service.Swarm.BlockChain;
                    var currency = new GoldCurrencyState(
                        (Dictionary)blockChain.GetState(GoldCurrencyState.Address)
                    ).Currency;
                    FungibleAssetValue amount =
                    FungibleAssetValue.Parse(currency, context.GetArgument<string>("amount"));

                    Address recipient = context.GetArgument<Address>("recipient");

                    blockChain.MakeTransaction(
                        privateKey,
                        new NCAction[]
                        {
                            new TransferAsset(
                                privateKey.ToAddress(),
                                recipient,
                                amount
                            ),
                        }
                    );
                    return true;
                }
            );
        }
    }
}
