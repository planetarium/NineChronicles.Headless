using System.Runtime.CompilerServices;
using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Explorer.GraphTypes;
using Nekoyume;
using Nekoyume.Helper;
using Nekoyume.Model.State;

namespace NineChronicles.Headless.GraphTypes
{
    public class AddressQuery : ObjectGraphType
    {
        public AddressQuery(StandaloneContext standaloneContext)
        {
            Field<NonNullGraphType<AddressType>>("raiderAddress")
                .Description("user information address by world boss season.")
                .Argument<Address>("avatarAddress", false, "address of avatar state.")
                .Argument<int>("raidId", false, "world boss season id.")
                .Resolve(context =>
                    Addresses.GetRaiderAddress(
                        context.GetArgument<Address>("avatarAddress"),
                        context.GetArgument<int>("raidId")));

            Field<NonNullGraphType<AddressType>>("worldBossAddress")
                .Description("boss information address by world boss season.")
                .Argument<int>("raidId", false, "world boss season id.")
                .Resolve(context =>
                    Addresses.GetWorldBossAddress(context.GetArgument<int>("raidId")));

            Field<NonNullGraphType<AddressType>>("worldBossKillRewardRecordAddress")
                .Description("user boss kill reward record address by world boss season.")
                .Argument<Address>("avatarAddress", false, "address of avatar state.")
                .Argument<int>("raidId", false, "world boss season id.")
                .Resolve(context =>
                    Addresses.GetWorldBossKillRewardRecordAddress(
                        context.GetArgument<Address>("avatarAddress"),
                        context.GetArgument<int>("raidId")));

            Field<NonNullGraphType<AddressType>>("raiderListAddress")
                .Description("raider list address by world boss season.")
                .Argument<int>("raidId", false, "world boss season id.")
                .Resolve(context =>
                    Addresses.GetRaiderListAddress(context.GetArgument<int>("raidId")));

            Field<ListGraphType<NonNullGraphType<AddressType>>>("currencyMintersAddress")
                .Description("currency minters address.")
                .Argument<CurrencyEnum>(
                    "currency",
                    false,
                    "A currency type. " +
                        "see also: https://github.com/planetarium/NineChronicles.Headless/blob/main/NineChronicles.Headless/GraphTypes/CurrencyEnumType.cs")
                .Resolve(context =>
                {
                    var currency = context.GetArgument<CurrencyEnum>("currency");
                    switch (currency)
                    {
                        case CurrencyEnum.NCG:
                            var blockchain = standaloneContext.BlockChain!;
                            var goldCurrencyStateDict =
                                (Dictionary)blockchain.GetState(Addresses.GoldCurrency);
                            var goldCurrencyState = new GoldCurrencyState(goldCurrencyStateDict);
                            return goldCurrencyState.Currency.Minters;
                        case CurrencyEnum.CRYSTAL:
                            return CrystalCalculator.CRYSTAL.Minters;
                        default:
                            throw new SwitchExpressionException(currency);
                    }
                });
        }
    }
}
