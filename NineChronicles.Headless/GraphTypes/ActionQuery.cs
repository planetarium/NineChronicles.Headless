using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Bencodex;
using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Assets;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Action;
using Nekoyume.Action.Factory;
using Nekoyume.Helper;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless.GraphTypes
{
    public class ActionQuery : ObjectGraphType
    {
        private static readonly Codec Codec = new Codec();
        internal StandaloneContext standaloneContext { get; set; }

        public ActionQuery(StandaloneContext standaloneContext)
        {
            this.standaloneContext = standaloneContext;

            Field<ByteStringType>("stake")
                .Argument<BigInteger>("amount", true, "An amount to stake.")
                .Resolve(context => Encode(
                    context,
                    (NCAction)new Stake(context.GetArgument<BigInteger>("amount"))));

            Field<ByteStringType>("claimStakeReward")
                .Argument<Address?>("avatarAddress", true, "The avatar address to receive staking rewards.")
                .Resolve(context =>
                {
                    if (!(standaloneContext.BlockChain is { } chain))
                    {
                        throw new InvalidOperationException("BlockChain not found in the context");
                    }

                    return Encode(
                        context,
                        (GameAction)ClaimStakeRewardFactory.CreateByBlockIndex(
                            chain.Tip.Index,
                            context.GetArgument<Address>("avatarAddress")));
                });
            Field<NonNullGraphType<ByteStringType>>("migrateMonsterCollection")
                .Argument<Address?>(
                    "avatarAddress",
                    true,
                    "The avatar address to receive monster collection rewards.")
                .Resolve(context =>
                    Encode(context,
                        (NCAction)new MigrateMonsterCollection(
                            context.GetArgument<Address>("avatarAddress"))));
            Field<ByteStringType>("grinding")
                .Argument<Address>(
                    "avatarAddress",
                    false,
                    "Address of avatar.")
                .Argument<NonNullGraphType<ListGraphType<GuidGraphType>>>(
                    "equipmentIds",
                    "List of equipment ItemId.")
                .Argument<bool?>(
                    "chargeAp",
                    true,
                    "Flag to Charge Action Point.")
                .Resolve(context =>
                {
                    var avatarAddress = context.GetArgument<Address>("avatarAddress");
                    var equipmentIds = context.GetArgument<List<Guid>>("equipmentIds");
                    var chargeAp = context.GetArgument<bool>("chargeAp");
                    NCAction action = new Grinding
                    {
                        AvatarAddress = avatarAddress,
                        EquipmentIds = equipmentIds,
                        ChargeAp = chargeAp,
                    };
                    return Encode(context, action);
                });
            Field<ByteStringType>("unlockEquipmentRecipe")
                .Argument<Address>("avatarAddress", false, "Address of avatar.")
                .Argument<NonNullGraphType<ListGraphType<IntGraphType>>>(
                    "recipeIds",
                    "List of EquipmentRecipeSheet row ids to unlock.")
                .Resolve(context =>
                {
                    var avatarAddress = context.GetArgument<Address>("avatarAddress");
                    var recipeIds = context.GetArgument<List<int>>("recipeIds");
                    NCAction action = new UnlockEquipmentRecipe
                    {
                        AvatarAddress = avatarAddress,
                        RecipeIds = recipeIds,
                    };
                    return Encode(context, action);
                });
            Field<ByteStringType>("unlockWorld")
                .Argument<Address>("avatarAddress", false, "Address of avatar.")
                .Argument<NonNullGraphType<ListGraphType<IntGraphType>>>(
                    "worldIds",
                    "List of WorldUnlockSheet row world_id_to_unlock.")
                .Resolve(context =>
                {
                    var avatarAddress = context.GetArgument<Address>("avatarAddress");
                    var worldIds = context.GetArgument<List<int>>("worldIds");
                    NCAction action = new UnlockWorld
                    {
                        AvatarAddress = avatarAddress,
                        WorldIds = worldIds,
                    };
                    return Encode(context, action);
                });
            Field<ByteStringType>("transferAsset")
                .Argument<Address>("sender", false, "Address of sender.")
                .Argument<Address>("recipient", false, "Address of recipient.")
                .Argument<string>("amount", false, "A string value to be transferred.")
                .Argument<NonNullGraphType<CurrencyEnumType>>("currency", "A currency type to be transferred.")
                .Argument<string?>("memo", true, "A 80-max length string to note.")
                .Resolve(context =>
                {
                    var sender = context.GetArgument<Address>("sender");
                    var recipient = context.GetArgument<Address>("recipient");
                    Currency currency = context.GetArgument<CurrencyEnum>("currency") switch
                    {
                        CurrencyEnum.NCG => new GoldCurrencyState(
                            (Dictionary)standaloneContext.BlockChain!.GetState(GoldCurrencyState.Address)
                        ).Currency,
                        CurrencyEnum.CRYSTAL => CrystalCalculator.CRYSTAL,
                        _ => throw new ExecutionError("Unsupported Currency type.")
                    };
                    var amount = FungibleAssetValue.Parse(currency, context.GetArgument<string>("amount"));
                    var memo = context.GetArgument<string?>("memo");
                    NCAction action = new TransferAsset(sender, recipient, amount, memo);
                    return Encode(context, action);
                });
            Field<NonNullGraphType<ByteStringType>>("patchTableSheet")
                .Argument<string>("tableName", false, "name of table sheet.")
                .Argument<string>("tableCsv", false, "table data.")
                .Resolve(context =>
                {
                    var tableName = context.GetArgument<string>("tableName");
                    var tableCsv = context.GetArgument<string>("tableCsv");
                    try
                    {
                        var _ = typeof(ISheet).Assembly
                            .GetTypes()
                            .First(type => type.Namespace is { } @namespace &&
                                           @namespace.StartsWith($"{nameof(Nekoyume)}.{nameof(Nekoyume.TableData)}") &&
                                           !type.IsAbstract &&
                                           typeof(ISheet).IsAssignableFrom(type) &&
                                           type.Name == tableName);
                    }
                    catch (Exception)
                    {
                        throw new ExecutionError("Invalid tableName.");
                    }

                    // TODO validate row data.
                    NCAction action = new PatchTableSheet
                    {
                        TableName = tableName,
                        TableCsv = tableCsv
                    };
                    return Encode(context, action);
                });
            Field<NonNullGraphType<ByteStringType>>("raid")
                .Argument<Address>("avatarAddress", false, "address of avatar state.")
                .Argument<ListGraphType<GuidGraphType>>(
                    "equipmentIds",
                    "list of equipment id.",
                    arg => arg.DefaultValue = new List<Guid>())
                .Argument<ListGraphType<GuidGraphType>>(
                    "costumeIds",
                    "list of costume id.",
                    arg => arg.DefaultValue = new List<Guid>())
                .Argument<ListGraphType<GuidGraphType>>(
                    "foodIds",
                    "list of food id.",
                    arg => arg.DefaultValue = new List<Guid>())
                .Argument<bool?>(
                    "payNcg",
                    true,
                    "refill ticket by NCG.",
                    arg => arg.DefaultValue = false)
                .Argument<ListGraphType<NonNullGraphType<RuneSlotInfoInputType>>>(
                    "runeSlotInfos",
                    "list of rune slot",
                    arg => arg.DefaultValue = new List<RuneSlotInfo>())
                .Resolve(context =>
                {
                    var avatarAddress = context.GetArgument<Address>("avatarAddress");
                    var equipmentIds = context.GetArgument<List<Guid>>("equipmentIds");
                    var costumeIds = context.GetArgument<List<Guid>>("costumeIds");
                    var foodIds = context.GetArgument<List<Guid>>("foodIds");
                    var payNcg = context.GetArgument<bool>("payNcg");
                    var runeSlotInfos = context.GetArgument<List<RuneSlotInfo>>("runeSlotInfos");

                    NCAction action = new Raid
                    {
                        AvatarAddress = avatarAddress,
                        EquipmentIds = equipmentIds,
                        CostumeIds = costumeIds,
                        FoodIds = foodIds,
                        PayNcg = payNcg,
                        RuneInfos = runeSlotInfos,
                    };
                    return Encode(context, action);
                });
            Field<NonNullGraphType<ByteStringType>>("claimRaidReward")
                .Argument<Address>(
                    "avatarAddress",
                    false,
                    "address of avatar state to receive reward.")
                .Resolve(context =>
                {
                    var avatarAddress = context.GetArgument<Address>("avatarAddress");

                    NCAction action = new ClaimRaidReward(avatarAddress);
                    return Encode(context, action);
                });
            Field<NonNullGraphType<ByteStringType>>("claimWorldBossKillReward")
                .Argument<Address>(
                    "avatarAddress",
                    false,
                    "address of avatar state to receive reward.")
                .Resolve(context =>
                {
                    var avatarAddress = context.GetArgument<Address>("avatarAddress");

                    NCAction action = new ClaimWordBossKillReward
                    {
                        AvatarAddress = avatarAddress,
                    };
                    return Encode(context, action);
                });
            Field<NonNullGraphType<ByteStringType>>("prepareRewardAssets")
                .Argument<Address>(
                    "rewardPoolAddress",
                    false,
                    "address of reward pool for charge reward.")
                .Argument<NonNullGraphType<ListGraphType<NonNullGraphType<FungibleAssetValueInputType>>>>(
                    "assets",
                    "list of FungibleAssetValue for charge reward.")
                .Resolve(context =>
                {
                    var assets = context.GetArgument<List<FungibleAssetValue>>("assets");
                    var rewardPoolAddress = context.GetArgument<Address>("rewardPoolAddress");

                    NCAction action = new PrepareRewardAssets
                    {
                        Assets = assets,
                        RewardPoolAddress = rewardPoolAddress,
                    };
                    return Encode(context, action);
                });
            Field<NonNullGraphType<ByteStringType>>("transferAssets")
                .Argument<Address>("sender", false, "Address of sender.")
                .Argument<NonNullGraphType<ListGraphType<NonNullGraphType<RecipientsInputType>>>>(
                    "recipients",
                    "List of tuples that recipients' address and asset amount to be sent")
                .Argument<string?>(
                    "memo",
                    true,
                    "A 80-max length string to note.")
                .Resolve(context =>
                {
                    var sender = context.GetArgument<Address>("sender");
                    var recipients =
                        context.GetArgument<List<(Address recipient, FungibleAssetValue amount)>>("recipients");
                    var memo = context.GetArgument<string?>("memo");
                    if (recipients.Count > TransferAssets.RecipientsCapacity)
                    {
                        throw new ExecutionError(
                            $"recipients must be less than or equal {TransferAssets.RecipientsCapacity}.");
                    }

                    NCAction action = new TransferAssets(sender, recipients, memo);
                    return Encode(context, action);
                }
            );
            Field<NonNullGraphType<ByteStringType>>("createAvatar")
                .Argument<int>(
                    "index",
                    false,
                    "index of avatar in `AgentState.avatarAddresses`.(0~2)")
                .Argument<string>(
                    "name",
                    false,
                    "name of avatar.(2~20 characters)")
                .Argument<int>(
                    "hair",
                    false,
                    "hair index of avatar.",
                    arg => arg.DefaultValue = 0)
                .Argument<int>(
                    "lens",
                    false,
                    "lens index of avatar.",
                    arg => arg.DefaultValue = 0)
                .Argument<int>(
                    "ear",
                    false,
                    "ear index of avatar.",
                    arg => arg.DefaultValue = 0)
                .Argument<int>(
                    "tail",
                    false,
                    "tail index of avatar.",
                    arg => arg.DefaultValue = 0)
                .Resolve(context =>
                {
                    var index = context.GetArgument<int>("index");
                    if (index < 0 || index > 2)
                    {
                        throw new ExecutionError(
                            $"Invalid index({index}). It must be 0~2.");
                    }

                    var name = context.GetArgument<string>("name");
                    if (name.Length < 2 || name.Length > 20)
                    {
                        throw new ExecutionError(
                            $"Invalid name({name}). It must be 2~20 characters.");
                    }

                    var hair = context.GetArgument<int>("hair");
                    var lens = context.GetArgument<int>("lens");
                    var ear = context.GetArgument<int>("ear");
                    var tail = context.GetArgument<int>("tail");

                    NCAction action = new CreateAvatar
                    {
                        index = index,
                        name = name,
                        hair = hair,
                        lens = lens,
                        ear = ear,
                        tail = tail,
                    };
                    return Encode(context, action);
                });
            Field<NonNullGraphType<ByteStringType>>("runeEnhancement")
                .Argument<Address>(
                    "avatarAddress",
                    false,
                    "The avatar address to enhance rune.")
                .Argument<int>(
                    "runeId",
                    false,
                    "Rune ID to enhance.")
                .Argument<int>(
                    "tryCount",
                    false,
                    "The try count to enhance rune",
                    arg => arg.DefaultValue = 1)
                .Resolve(context =>
                {
                    var avatarAddress = context.GetArgument<Address>("avatarAddress");
                    var runeId = context.GetArgument<int>("runeId");
                    var tryCount = context.GetArgument<int>("tryCount");
                    if (tryCount <= 0)
                    {
                        throw new ExecutionError($"tryCount must be positive: {tryCount} is invalid.");
                    }

                    NCAction action = new RuneEnhancement
                    {
                        AvatarAddress = avatarAddress,
                        RuneId = runeId,
                        TryCount = tryCount
                    };
                    return Encode(context, action);
                });
        }

        internal virtual byte[] Encode(IResolveFieldContext context, NCAction action)
        {
            return Codec.Encode(action.PlainValue);
        }
    }
}
