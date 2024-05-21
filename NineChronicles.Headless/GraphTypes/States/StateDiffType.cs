using Bencodex;
using Bencodex.Types;
using GraphQL.Types;
using Libplanet.Common;

namespace NineChronicles.Headless.GraphTypes.States;

public class StateDiffType : ObjectGraphType<StateDiffType.Value>
{
    public class Value
    {
        public string Path { get; }
        public IValue? BaseState { get; }
        public IValue? ChangedState { get; }

        public Value(string path, IValue? baseState, IValue? changedState)
        {
            Path = path;
            BaseState = baseState;
            ChangedState = changedState;
        }

        public bool IsAddressRelated(string address) => Path.Contains(address);
    }

    public StateDiffType()
    {
        Name = "StateDiff";

        Field<NonNullGraphType<StringGraphType>>(
            "Path",
            description: "The path of the state difference."
        );

        Field<StringGraphType>(
            "BaseState",
            description: "The base state before changes.",
            resolve: context =>
                context.Source.BaseState is null
                    ? null
                    : ByteUtil.Hex(new Codec().Encode(context.Source.BaseState))
        );

        Field<StringGraphType>(
            "ChangedState",
            description: "The state after changes.",
            resolve: context =>
                context.Source.ChangedState is null
                    ? null
                    : ByteUtil.Hex(new Codec().Encode(context.Source.ChangedState))
        );
    }
}
