#if LIB9C_DEV_EXTENSIONS
using System;
using System.Text.RegularExpressions;
using GraphQL;
using GraphQL.Types;
using Lib9c.DevExtensions.Action.Factory;
using Libplanet.Explorer.GraphTypes;
using Nekoyume;
using Nekoyume.Action;
using NineChronicles.Headless.GraphTypes.Input;

namespace NineChronicles.Headless.GraphTypes
{
    public partial class ActionQuery : ObjectGraphType
    {
        private void RegisterFieldsForDevEx()
        {
            Field<NonNullGraphType<ByteStringType>>(
                "createOrReplaceAvatar",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "avatarIndex",
                        Description = "index of avatar in `AgentState.avatarAddresses`.(0~2)",
                    },
                    new QueryArgument<StringGraphType>
                    {
                        Name = "name",
                        Description = "name of avatar. default is `Avatar-{avatarIndex:00}.`",
                    },
                    new QueryArgument<IntGraphType>
                    {
                        Name = "hair",
                        Description = "hair index of avatar. default is 0.",
                        DefaultValue = 0,
                    },
                    new QueryArgument<IntGraphType>
                    {
                        Name = "lens",
                        Description = "lens index of avatar. default is 0.",
                        DefaultValue = 0,
                    },
                    new QueryArgument<IntGraphType>
                    {
                        Name = "ear",
                        Description = "ear index of avatar. default is 0.",
                        DefaultValue = 0,
                    },
                    new QueryArgument<IntGraphType>
                    {
                        Name = "tail",
                        Description = "tail index of avatar. default is 0.",
                        DefaultValue = 0,
                    },
                    new QueryArgument<IntGraphType>
                    {
                        Name = "level",
                        Description = "level of avatar. default is 1.",
                        DefaultValue = 1,
                    },
                    new QueryArgument<ListGraphType<ItemIdAndEnhancementType>>
                    {
                        Name = "equipments",
                        Description = "equipments of avatar. default is null.",
                        DefaultValue = null,
                    }),
                resolve: context =>
                {
                    if (StandaloneContext.BlockChain is not { } chain)
                    {
                        throw new InvalidOperationException(
                            "BlockChain not found in the context");
                    }

                    var avatarIndex = context.GetArgument<int>("avatarIndex");
                    if (avatarIndex is < 0 or > 2)
                    {
                        throw new ExecutionError(
                            $"Invalid index({avatarIndex}). It must be 0~2.");
                    }

                    var name = context.HasArgument("name")
                        ? context.GetArgument<string>("name")
                        : $"Avatar{avatarIndex:00}";
                    if (!Regex.IsMatch(name, GameConfig.AvatarNickNamePattern))
                    {
                        throw new ExecutionError(
                            $"Invalid name({name}). It must match the regex: {Regex.Escape(GameConfig.AvatarNickNamePattern)}");
                    }

                    var (exception, result) =
                        CreateOrReplaceAvatarFactory.TryGetByBlockIndex(
                            avatarIndex: avatarIndex,
                            name: name,
                            hair: context.GetArgument<int>("hair"),
                            lens: context.GetArgument<int>("lens"),
                            ear: context.GetArgument<int>("ear"),
                            tail: context.GetArgument<int>("tail"),
                            level: context.GetArgument<int>("level"),
                            equipments: context.GetArgument<(int, int)[]>("equipments"));
                    if (exception is { })
                    {
                        throw exception;
                    }

                    var action = (GameAction)result!;
                    return Encode(context, action);
                });
        }
    }
}
#endif
