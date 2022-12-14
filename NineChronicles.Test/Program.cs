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
            Console.WriteLine("Waiting for tx confirmed...");
            string txResult = await Graphql.TxResult(txId);
            while (txResult == "STAGING")
            {
                txResult = await Graphql.TxResult(txId);
            }

            Console.WriteLine("Account activated.");
        }
    }
}
