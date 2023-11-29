using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Bencodex;
using Bencodex.Types;
using GraphQL.Execution;
using Libplanet.Common;
using Libplanet.Crypto;
using Nekoyume.Action;
using NineChronicles.Headless.GraphTypes;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

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
            var avatarAddress = new PrivateKey().Address;
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
            var actionBase = DeserializeNCAction(plainValue);
            var action = Assert.IsType<EventConsumableItemCrafts>(actionBase);
            Assert.Equal(avatarAddress, action.AvatarAddress);
            Assert.Equal(eventScheduleId, action.EventScheduleId);
            Assert.Equal(eventConsumableItemRecipeId, action.EventConsumableItemRecipeId);
            Assert.Equal(slotIndex, action.SlotIndex);
        }

        [Fact]
        public async Task EventMaterialItemCrafts()
        {
            var avatarAddress = new PrivateKey().Address;
            var eventScheduleId = 1;
            var eventMaterialItemRecipeId = 10;
            var MaterialsToUse = new Dictionary<int, int>
            {
                { 1, 1 },
                { 2, 2 },
                { 3, 3 }
            };

            var materialArg = new StringBuilder("[");
            foreach (var material in MaterialsToUse)
            {
                materialArg.Append($"{{materialId: {material.Key}, quantity: {material.Value}}},");
            }

            materialArg.Remove(materialArg.Length - 1, 1);
            materialArg.Append("]");

            var query = $@"
            {{
                craftQuery
                {{
                    eventMaterialItemCrafts(
                        avatarAddress: ""{avatarAddress}"",
                        eventScheduleId: {eventScheduleId},
                        eventMaterialItemRecipeId: {eventMaterialItemRecipeId},
                        materialsToUse: {materialArg}
                    )
                }}
            }}";
            var queryResult = await ExecuteQueryAsync<ActionQuery>(query, standaloneContext: _standaloneContext);
            Assert.Null(queryResult.Errors);

            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var craftQueryData = (Dictionary<string, object>)data["craftQuery"];
            var plainValue = _codec.Decode(ByteUtil.ParseHex((string)craftQueryData["eventMaterialItemCrafts"]));
            Assert.IsType<Dictionary>(plainValue);
            var actionBase = DeserializeNCAction(plainValue);
            var action = Assert.IsType<EventMaterialItemCrafts>(actionBase);
            Assert.Equal(avatarAddress, action.AvatarAddress);
            Assert.Equal(eventScheduleId, action.EventScheduleId);
            Assert.Equal(eventMaterialItemRecipeId, action.EventMaterialItemRecipeId);
            Assert.Equal(MaterialsToUse, action.MaterialsToUse);
        }
    }
}
