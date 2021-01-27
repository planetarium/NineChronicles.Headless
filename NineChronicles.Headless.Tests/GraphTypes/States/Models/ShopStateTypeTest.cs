using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Bencodex.Types;
using Libplanet;
using Libplanet.Action;
using Libplanet.Assets;
using Libplanet.Crypto;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Model.Item;
using Nekoyume.Model.State;
using Xunit;
using Xunit.Abstractions;

namespace NineChronicles.Headless.Tests.GraphTypes.States.Models
{
    public class ShopStateTypeTest: GraphQLTestBase
    {
        public ShopStateTypeTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task StateQueryShopStateNull()
        {
            var userPrivateKey = new PrivateKey();
            var userAddress = userPrivateKey.PublicKey.ToAddress();
            var avatarAddress = userAddress.Derive(string.Format(CultureInfo.InvariantCulture,
                CreateAvatar2.DeriveFormat, 0));
            StandaloneContextFx.BlockChain = BlockChain;

            const string query = @"query {
                stateQuery {
                    shop {
                        address
                        products {
                            sellerAgentAddress
                            sellerAvatarAddress
                            price
                            itemUsable {
                                itemId
                                itemType
                                itemSubType
                            }
                            costume {
                                itemId
                                itemType
                                itemSubType
                            }
                        }
                    }
                }
            }";
            var queryResult = await ExecuteQueryAsync(query);
            Assert.Equal(
                new Dictionary<string, object>
                {
                    ["stateQuery"] = new Dictionary<string, object>
                    {
                        ["shop"] = new Dictionary<string, object>
                        {
                            ["address"] = Addresses.Shop.ToString(),
                            ["products"] = new List<object>()
                        },
                    },
                },
                queryResult.Data
            );
        }

        [Theory]
        [MemberData(nameof(Members))]
        public async Task StateQueryShopState(
            Guid itemId,
            Dictionary<string, object> itemUsable,
            Dictionary<string, object> costume
        )
        {
            var userPrivateKey =
                new PrivateKey(ByteUtil.ParseHex("b934cb79757b1dec9f89caa01c4b791a6de6937dbecdc102fbdca217156cc2f5"));
            var minerAddress = new PrivateKey().PublicKey.ToAddress();
            var avatarAddress = new Address("983c3Fbfe8243a0e36D55C6C1aE26A7c8Bb6CBd4");

            const string query = @"query {
                stateQuery {
                    shop {
                        address
                        products {
                            sellerAgentAddress
                            sellerAvatarAddress
                            price
                            itemUsable {
                                itemId
                                itemType
                                itemSubType
                            }
                            costume {
                                itemId
                                itemType
                                itemSubType
                            }
                        }
                    }
                }
            }";

            var action = new SetAvatarState();
            BlockChain.MakeTransaction(
                userPrivateKey,
                new PolymorphicAction<ActionBase>[] { action }
            );
            await BlockChain.MineBlock(minerAddress);

            var avatarState = new AvatarState((Dictionary)BlockChain.GetState(avatarAddress));
            var expected = new Dictionary<string, object>
            {
                ["stateQuery"] = new Dictionary<string, object>
                {
                    ["shop"] = new Dictionary<string, object>
                    {
                        ["address"] = Addresses.Shop.ToString(),
                        ["products"] = new List<object>
                        {
                            new Dictionary<string, object>
                            {
                                ["sellerAgentAddress"] = "0xfc2a412ea59122B114B672a5518Bc113955Dd2FE",
                                ["sellerAvatarAddress"] = "0x983c3Fbfe8243a0e36D55C6C1aE26A7c8Bb6CBd4",
                                ["price"] = "100 NCG",
                                ["itemUsable"] = itemUsable,
                                ["costume"] = costume,
                            }
                        },
                    },
                },
            };

            var action2 = new Sell3
            {
                sellerAvatarAddress = avatarAddress,
                itemId = itemId,
                price = new Currency("NCG", 2, minter: null) * 100
            };
            BlockChain.MakeTransaction(
                userPrivateKey,
                new PolymorphicAction<ActionBase>[] { action2 }
            );
            await BlockChain.MineBlock(minerAddress);
            var queryResult = await ExecuteQueryAsync(query);
            Assert.Equal(expected, queryResult.Data);
        }

        public static IEnumerable<object[]> Members => new List<object[]>
        {
            new object[]
            {
                new Guid("220acb43-095e-46f6-9725-4223c69827e8"),
                new Dictionary<string, object>
                {
                    ["itemId"] = new Guid("220acb43-095e-46f6-9725-4223c69827e8"),
                    ["itemType"] = ItemType.Equipment.ToString().ToUpper(),
                    ["itemSubType"] = ItemSubType.Weapon.ToString().ToUpper(),
                },
                null,
            },
            new object[]
            {
                new Guid("d3d9ac06-eb91-4cc4-863a-5b4769ad633e"),
                null,
                new Dictionary<string, object>
                {
                    ["itemId"] = new Guid("d3d9ac06-eb91-4cc4-863a-5b4769ad633e"),
                    ["itemType"] = ItemType.Costume.ToString().ToUpper(),
                    ["itemSubType"] = "FULL_COSTUME",
                },
            },
        };
    }
}
