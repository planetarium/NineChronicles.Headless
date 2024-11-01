using System;
using System.Linq;
using Bencodex.Types;
using Libplanet.Action.State;
using Libplanet.Action;
using Libplanet.Types.Assets;
using Libplanet.Types.Consensus;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Model.State;
using Nekoyume.Module;
using Nekoyume.Module.ValidatorDelegation;
using Nekoyume.ValidatorDelegation;

namespace NineChronicles.Headless.Tests.Common.Actions
{
    [ActionType("initialize_validator")]
    public sealed class InitializeValidator : ActionBase
    {
        public InitializeValidator(
            ValidatorSet validatorSet,
            Currency goldCurrency)
        {
            Validators = validatorSet.Validators.ToArray();
            GoldCurrency = goldCurrency;
        }

        public Validator[] Validators { get; set; }

        public Currency GoldCurrency { get; set; }

        public override IValue PlainValue
            => Dictionary.Empty
                .Add("validator_set", new List(Validators.Select(item => item.Bencoded)))
                .Add("gold_currency", GoldCurrency.Serialize());

        public override void LoadPlainValue(IValue value)
        {
            if (value is not Dictionary dict ||
                dict["validator_set"] is not List list ||
                dict["gold_currency"] is not Dictionary currencyDict)
            {
                throw new InvalidCastException("Invalid types");
            }

            Validators = list.Select(item => new Validator((Dictionary)item)).ToArray();
            GoldCurrency = new Currency(currencyDict);
        }

        public override IWorld Execute(IActionContext context)
        {
            var world = context.PreviousState;

            var goldCurrency = GoldCurrency;
            var currencyState = new GoldCurrencyState(goldCurrency);
            world = world
                .SetLegacyState(GoldCurrencyState.Address, currencyState.Serialize())
                .SetLegacyState(Addresses.GoldDistribution, new List().Serialize());

            if (currencyState.InitialSupply > 0)
            {
                world = world.MintAsset(
                    context,
                    GoldCurrencyState.Address,
                    currencyState.Currency * currencyState.InitialSupply);
            }

            var repository = new ValidatorRepository(world, context);
            var validators = Validators;
            foreach (var validator in validators)
            {
                var validatorDelegatee = new ValidatorDelegatee(
                    validator.OperatorAddress,
                    validator.PublicKey,
                    ValidatorDelegatee.DefaultCommissionPercentage,
                    context.BlockIndex,
                    repository);
                var delegationFAV = FungibleAssetValue.FromRawValue(
                        validatorDelegatee.DelegationCurrency, validator.Power);
                var validatorOperatorAddress = validator.OperatorAddress;
                var validatorDelegator = repository.GetValidatorDelegator(
                    validatorOperatorAddress, validatorOperatorAddress);

                repository.SetValidatorDelegatee(validatorDelegatee);
                repository.UpdateWorld(
                    repository.World.MintAsset(
                        repository.ActionContext,
                        validatorDelegator.DelegationPoolAddress,
                        delegationFAV));
                validatorDelegator.Delegate(validatorDelegatee, delegationFAV, context.BlockIndex);
            }

            repository.SetAbstainHistory(new());
            world = repository.World;

            return world;
        }
    }
}
