using System.Numerics;
using Bencodex;
using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Action;

namespace NineChronicles.Headless.GraphTypes
{
    public class ActionQuery : ObjectGraphType
    {
        public ActionQuery()
        {
            var codec = new Codec();
            Field<ByteStringType>(
                name: "stake",
                arguments: new QueryArguments(new QueryArgument<BigIntGraphType>
                {
                    Name = "amount",
                    Description = "An amount to stake.",
                }),
                resolve: context => codec.Encode(new Stake(context.GetArgument<BigInteger>("amount")).PlainValue));

            Field<ByteStringType>(
                name: "claimStakeReward",
                arguments: new QueryArguments(
                    new QueryArgument<AddressType>
                    {
                        Name = "avatarAddress",
                        Description = "The avatar address to receive staking rewards."
                    }),
                resolve: context =>
                    codec.Encode(new ClaimStakeReward(context.GetArgument<Address>("avatarAddress")).PlainValue));
        }
    }
}
