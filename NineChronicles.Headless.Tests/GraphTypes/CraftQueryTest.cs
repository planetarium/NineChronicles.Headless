using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bencodex;
using Bencodex.Types;
using GraphQL.Execution;
using Libplanet;
using Libplanet.Crypto;
using Nekoyume.Action;
using NineChronicles.Headless.GraphTypes;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless.Tests.GraphTypes
{
    public class CraftQueryTest
    {
        private readonly Codec _codec;
        private readonly StandaloneContext _standaloneContext;

        public CraftQueryTest()
        {
            _codec = new Codec();
            _standaloneContext = CreateStandaloneContext();
        }

        [Fact]
        public async Task EventConsumableItemCrafts()
        {
            var avatarAddress = new PrivateKey().ToAddress();
            var eventScheduleId = 1;
            var eventConsumableItemRecipeId = 10;
            var slotIndex = 0;

            var query = $@"
            {{
                craftQuery{{
                    eventConsumableItemCrafts(
                    avatarAddress: ""{avatarAddress}"",
                    eventScheduleId: {eventScheduleId},
                    eventConsumableItemRecipeId: {eventConsumableItemRecipeId},
                    slotIndex: {slotIndex}
                    )
                }}
            }}";
            var queryResult = await ExecuteQueryAsync<ActionQuery>(query, standaloneContext: _standaloneContext);
            Assert.Null(queryResult.Errors);

            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var craftQueryData = (Dictionary<string, object>)data["craftQuery"];
            var plainValue = _codec.Decode(ByteUtil.ParseHex((string)craftQueryData["eventConsumableItemCrafts"]));
            Assert.IsType<Dictionary>(plainValue);
            var polymorphicAction = DeserializeNCAction(plainValue);
            var action = Assert.IsType<EventConsumableItemCrafts>(polymorphicAction.InnerAction);
            Assert.Equal(avatarAddress, action.AvatarAddress);
            Assert.Equal(eventScheduleId, action.EventScheduleId);
            Assert.Equal(eventConsumableItemRecipeId, action.EventConsumableItemRecipeId);
            Assert.Equal(slotIndex, action.SlotIndex);
        }
    }
}
