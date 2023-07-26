using System;
using Bencodex.Types;
using Lib9c;
using Libplanet.Action.State;
using Libplanet.Types.Assets;
using Nekoyume;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes;

namespace NineChronicles.Headless.Utils;

public class CurrencyFactory
{
    private readonly Func<IAccountState> _accountStateGetter;
    private Currency? _ncg;

    public CurrencyFactory(
        Func<IAccountState> accountStateGetter,
        Currency? ncg = null)
    {
        _accountStateGetter = accountStateGetter;
        _ncg = ncg;
    }

    public bool TryGetCurrency(CurrencyEnum currencyEnum, out Currency currency)
    {
        return TryGetCurrency(currencyEnum.ToString(), out currency);
    }

    public bool TryGetCurrency(string ticker, out Currency currency)
    {
        var result = ticker switch
        {
            "NCG" => GetNCG(),
            _ => Currencies.GetMinterlessCurrency(ticker),
        };
        if (result is null)
        {
            currency = default;
            return false;
        }

        currency = result.Value;
        return true;
    }

    private Currency? GetNCG()
    {
        if (_ncg is not null)
        {
            return _ncg;
        }

        var value = _accountStateGetter().GetState(Addresses.GoldCurrency);
        if (value is Dictionary goldCurrencyDict)
        {
            var goldCurrency = new GoldCurrencyState(goldCurrencyDict);
            _ncg = goldCurrency.Currency;
        }

        return _ncg;
    }
}
