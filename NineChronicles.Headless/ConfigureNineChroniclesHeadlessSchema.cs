using System;
using GraphQL;
using GraphQL.DI;
using GraphQL.Types;
using Libplanet.Assets;
using Libplanet.Headless;
using Libplanet.Net;
using Libplanet.Tx;
using Nekoyume.Model.Item;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes;
using NineChronicles.Headless.GraphTypes.States;
using NineChronicles.Headless.GraphTypes.States.Models.Item.Enum;
using CurrencyType = Libplanet.Explorer.GraphTypes.CurrencyType;
using NodeExceptionType = NineChronicles.Headless.GraphTypes.NodeExceptionType;

namespace NineChronicles.Headless;

public class ConfigureNineChroniclesHeadlessSchema : IConfigureSchema
{
    public static readonly ConfigureNineChroniclesHeadlessSchema Instance = new();

    public void Configure(ISchema schema, IServiceProvider serviceProvider)
    {
        schema.RegisterTypeMapping<TxId, TxIdType>();
        schema.RegisterTypeMapping<Currency, CurrencyType>();
        schema.RegisterTypeMapping<StandaloneContext, NodeStatusType>();
        schema.RegisterTypeMapping<StandaloneSubscription.Tip, StandaloneSubscription.TipChanged>();
        schema.RegisterTypeMapping<PreloadState, StandaloneSubscription.PreloadStateType>();
        schema.RegisterTypeMapping<DifferentAppProtocolVersionEncounter, DifferentAppProtocolVersionEncounterType>();
        schema.RegisterTypeMapping<Notification, NotificationType>();
        schema.RegisterTypeMapping<NodeException, NodeExceptionType>();
        schema.RegisterTypeMapping<MonsterCollectionState, MonsterCollectionStateType>();
        schema.RegisterTypeMapping<MonsterCollectionStatus, MonsterCollectionStatusType>();
        schema.RegisterTypeMapping<ItemType, ItemTypeEnumType>();
        schema.RegisterTypeMapping<ItemSubType, ItemSubTypeEnumType>();
        schema.RegisterTypeMapping<CurrencyEnum, CurrencyEnumType>();
    }
}
