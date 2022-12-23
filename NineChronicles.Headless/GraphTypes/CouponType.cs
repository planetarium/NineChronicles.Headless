using GraphQL.Types;
using Nekoyume.Model.Coupons;

namespace NineChronicles.Headless.GraphTypes
{
    public class CouponType : ObjectGraphType<Coupon>
    {
        public CouponType()
        {
            Field<NonNullGraphType<GuidGraphType>>(
                nameof(Coupon.Id),
                resolve: context => context.Source.Id
            );
            Field<NonNullGraphType<ListGraphType<RewardSetItemPairType>>>(
                nameof(Coupon.Rewards),
                resolve: context => context.Source.Rewards
            );
        }
    }
}
