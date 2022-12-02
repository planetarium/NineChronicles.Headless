using GraphQL.Types;

namespace NineChronicles.Headless.GraphTypes
{
    public sealed class DifferentAppProtocolVersionEncounterType
        : ObjectGraphType<DifferentAppProtocolVersionEncounter>
    {
        public DifferentAppProtocolVersionEncounterType()
        {
            Field<NonNullGraphType<StringGraphType>>("peer")
                .Resolve(context => context.Source.Peer.ToString());
            Field<NonNullGraphType<AppProtocolVersionType>>("peerVersion")
                .Resolve(context => context.Source.PeerVersion);
            Field<NonNullGraphType<AppProtocolVersionType>>("localVersion")
                .Resolve(context => context.Source.LocalVersion);
        }
    }
}
