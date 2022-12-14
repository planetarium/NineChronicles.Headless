using Libplanet.Crypto;
using Libplanet.KeyStore;

namespace NineChronicles.Test;

public static class Address
{
    public static PrivateKey GetKey()
    {
        var keystore = Web3KeyStore.DefaultKeyStore;
        Console.WriteLine("Pick your 9c address to test action:");
        Console.WriteLine("0: Create New Address");
        foreach (var item in keystore.List().Select((value, index) => (value, index)))
        {
            Console.WriteLine($"{item.index + 1}: {item.value.Item2.Address.ToString()}");
        }

        Console.WriteLine();

        bool usable = false;
        int index = -1;
        while (!usable)
        {
            var selectedIndex = Console.ReadLine();
            usable = int.TryParse(selectedIndex, out index);
            if (!usable)
            {
                Console.WriteLine("Please input number.");
            }
            else if (index > keystore.List().Count())
            {
                Console.WriteLine($"{index} is not on the list. Please set right one: ");
                usable = false;
            }
        }

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
            Console.WriteLine("New key generated.");
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
