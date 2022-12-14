using Libplanet;

namespace NineChronicles.Test;

class Program
{
    static async Task Main(string[] args)
    {
        var pk = await Address.GetKey();
        if (await Graphql.GetNextTxNonce(pk.ToAddress()) == 0)
        {
            Console.WriteLine("Account activation required. Please input activation code");
            var activationCode = Console.ReadLine();
            string txId = await Address.ActivateAccount(pk, activationCode!);
            string txResult = await Graphql.WaitTxMining(txId);
            if (txResult == "SUCCESS")
            {
                Console.WriteLine("Account activated.");
            }
            else
            {
                Console.WriteLine("Account activation failed. Please try with another activation code");
            }
        }

        var avatarAddress = await Avatar.SelectAvatar(pk);
        if (avatarAddress is null)
        {
            Console.WriteLine("No avatar selected. Exiting...");
            return;
        }
        // TODO: Do action
    }
}
