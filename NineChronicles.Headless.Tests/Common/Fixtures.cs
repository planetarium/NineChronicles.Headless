using System;
using System.IO;
using System.Linq;
using Libplanet;
using Libplanet.Assets;
using Libplanet.Crypto;
using Nekoyume.Model.Item;
using Nekoyume.Model.State;

namespace NineChronicles.Headless.Tests
{
    public static class Fixtures
    {
        public static readonly PrivateKey UserPrivateKey =
            new PrivateKey(ByteUtil.ParseHex("b934cb79757b1dec9f89caa01c4b791a6de6937dbecdc102fbdca217156cc2f5"));

        public static readonly Address MinerAddress = new PrivateKey().PublicKey.ToAddress();

        public static readonly Address UserAddress = UserPrivateKey.PublicKey.ToAddress();

        public static readonly Address AvatarAddress = new Address("983c3Fbfe8243a0e36D55C6C1aE26A7c8Bb6CBd4");

        public static readonly TableSheets TableSheetsFX = new TableSheets(TableSheetsImporter.ImportSheets(
            Path.Combine("..", "..", "..", "..", "Lib9c", ".Lib9c.Tests", "Data", "TableCSV")));

        public static readonly AvatarState AvatarStateFX = new AvatarState(
            AvatarAddress,
            UserAddress,
            0,
            TableSheetsFX.GetAvatarSheets(),
            new GameConfigState(),
            new Address(),
            "avatar_state_fx"
        );

        public static readonly Currency CurrencyFX = new Currency("NCG", 2, minter: null);

        public static ShopState ShopStateFX()
        {
            var shopState = new ShopState();
            for (var index = 0; index < TableSheetsFX.EquipmentItemSheet.OrderedList.Count; index++)
            {
                var row = TableSheetsFX.EquipmentItemSheet.OrderedList[index];
                var equipment = ItemFactory.CreateItemUsable(row, default, 0);
                var shopItem = new ShopItem(UserAddress, AvatarAddress, Guid.NewGuid(), index * CurrencyFX, equipment);
                shopState.Register(shopItem);
            }

            for (var i = 0; i < TableSheetsFX.CostumeItemSheet.OrderedList.Count; i++)
            {
                var row = TableSheetsFX.CostumeItemSheet.OrderedList[i];
                var equipment = ItemFactory.CreateCostume(row, default);
                var shopItem = new ShopItem(UserAddress, AvatarAddress, Guid.NewGuid(), i * CurrencyFX, equipment);
                shopState.Register(shopItem);
            }
            return shopState;
        }
    }
}
