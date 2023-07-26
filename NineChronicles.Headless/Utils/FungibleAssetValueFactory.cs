using System.Numerics;
using Libplanet.Types.Assets;
using NineChronicles.Headless.GraphTypes;

namespace NineChronicles.Headless.Utils;

public class FungibleAssetValueFactory
{
    private readonly CurrencyFactory _currencyFactory;

    public FungibleAssetValueFactory(CurrencyFactory currencyFactory)
    {
        _currencyFactory = currencyFactory;
    }

    public bool TryGetFungibleAssetValue(
        CurrencyEnum currencyEnum,
        BigInteger majorUnit,
        BigInteger minorUnit,
        out FungibleAssetValue fungibleAssetValue)
    {
        return TryGetFungibleAssetValue(
            currencyEnum.ToString(),
            majorUnit,
            minorUnit,
            out fungibleAssetValue);
    }

    public bool TryGetFungibleAssetValue(
        string ticker,
        BigInteger majorUnit,
        BigInteger minorUnit,
        out FungibleAssetValue fungibleAssetValue)
    {
        if (!_currencyFactory.TryGetCurrency(ticker, out var currency))
        {
            fungibleAssetValue = default;
            return false;
        }

        fungibleAssetValue = new FungibleAssetValue(currency, majorUnit, minorUnit);
        return true;
    }

    public bool TryGetFungibleAssetValue(
        CurrencyEnum currencyEnum,
        string value,
        out FungibleAssetValue fungibleAssetValue)
    {
        return TryGetFungibleAssetValue(
            currencyEnum.ToString(),
            value,
            out fungibleAssetValue);
    }

    public bool TryGetFungibleAssetValue(
        string ticker,
        string value,
        out FungibleAssetValue fungibleAssetValue)
    {
        if (!_currencyFactory.TryGetCurrency(ticker, out var currency))
        {
            fungibleAssetValue = default;
            return false;
        }

        fungibleAssetValue = FungibleAssetValue.Parse(currency, value);
        return true;
    }
}
