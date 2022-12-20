using GraphQL;
using Libplanet;
using Libplanet.Crypto;
using Libplanet.KeyStore;
using NineChronicles.Test.Type;

namespace NineChronicles.Test;

public static class Address
{
    public static async Task<string?> ActivateAccount(PrivateKey pk, string activationCode)
    {
        var query = $"activateAccount (activationCode: \"{activationCode}\")";
        (bool success, ActionTxQueryResponseType data, GraphQLError[]? errors) = await Graphql.Action(pk, query);
        if (!success)
        {
            Console.WriteLine(errors);
            return null;
        }

        var tx = ByteUtil.ParseHex(data.ActionTxQuery.ActivateAccount);
        var signature = pk.Sign(tx);
        (bool result, string txId) = await Graphql.Stage(tx, signature);
        Console.WriteLine($"Account activation: {result} :: TxID {txId}");
        if (!result)
        {
            return null;
        }

        return txId;
    }

    public static async Task<PrivateKey> GetKey()
    {
        var keystore = Web3KeyStore.DefaultKeyStore;
        Console.WriteLine("Pick your 9c address to test action:");
        Console.WriteLine("0: Create New Address");
        foreach (var item in keystore.List().Select((value, index) => (value, index)))
        {
            Console.WriteLine($"{item.index + 1}: {item.value.Item2.Address.ToString()}");
        }

        var index = Util.Select(keystore.List());

        if (index == 0)
        {
            var pk = new PrivateKey();
            string? passphrase = String.Empty;
            while (true)
            {
                Console.Write("Passphrase: ");
                passphrase = Console.ReadLine();
                Console.Write("Passphrase again: ");
                var check = Console.ReadLine();
                if (passphrase == check)
                {
                    break;
                }

                Console.WriteLine("Passphrase not matched. Try again.");
            }

            var ppk = ProtectedPrivateKey.Protect(pk, passphrase);
            keystore.Add(ppk);
            Console.WriteLine("New address generated.");
            Console.WriteLine($"Your address is: {ppk.Address} . Now preparing account to do action...");
            return pk;
        }

        while (true)
        {
            var ppk = keystore.List().ElementAt(index - 1).Item2;
            Console.Write("Passphrase: ");
            try
            {
                var pk = ppk.Unprotect(Console.ReadLine());
                Console.WriteLine("Address unlocked. Now preparing account to do action...");
                return pk;
            }
            catch (Exception e) when (e is IncorrectPassphraseException || e is MismatchedAddressException)
            {
                Console.WriteLine("Failed to unlock your address.");
            }
        }
    }
}
