using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using GraphQL;
using GraphQL.Types;
using Lib9c;
using Libplanet;
using Libplanet.Explorer.GraphTypes;
using Nekoyume;
using NineChronicles.Headless.GraphTypes.States;

namespace NineChronicles.Headless.GraphTypes
{
    public partial class StateQuery
    {
        private void RegisterGarages()
        {
            Field<GarageStateType>(
                "garages",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "agentAddr",
                        Description = "Address to get GARAGE token balance and fungible items"
                    },
                    new QueryArgument<ListGraphType<NonNullGraphType<StringGraphType>>>
                    {
                        Name = "fungibleIds",
                        Description = "List of fungible item IDs to get stock in garage"
                    }
                ),
                resolve: context =>
                {
                    var agentAddr = context.GetArgument<Address>("agentAddr");
                    var balance = context.Source.GetBalance(agentAddr, Currencies.Garage);
                    var fungibleItemIdList = context.GetArgument<IEnumerable<string>>("fungibleIds");
                    IEnumerable<Address> fungibleItemAddressList = fungibleItemIdList.Select(fungibleItemId =>
                        Addresses.GetGarageAddress(agentAddr, HashDigest<SHA256>.FromString(fungibleItemId)));
                    var fungibleItemList = context.Source.GetStates(fungibleItemAddressList.ToArray());
                    return (balance, fungibleItemList);
                }
            );
        }
    }
}
