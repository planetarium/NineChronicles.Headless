# Build Genesis Block with configuration

Hello, you can learn how to build genesis block with NineChronicles.Headless.Executable `genesis` command from this guide.

## 0. Clone repositories

At first, you should clone repositories related to NineChronicles. The base directory is not important.

```shell
cd /tmp
git clone https://github.com/planetarium/lib9c
git clone https://github.com/planetarium/NineChronicles
git clone https://github.com/planetarium/NineChronicles.Headless
```

## 1. Prepare genesis block configuration.

You should prepare genesis block configuration of a genesis block.

Copy below content and save as `/tmp/config.json` or other path.
Of course, you can replace the fields with your private key in your favor.

```json
{
    "tablePath": "/tmp/lib9c/Lib9c/TableCSV",
    "privateKey": "e87a05d05506b73570e80f6e99beeceae6a9891333b2e8e8951197050fad96e2",
    "adminAddress": "2c2A05E29e8f57C4661Fb8FFf5e0C7A7e0f3c4Fc",
    "adminValidUntil": 10000000,
    "goldDistributionPath": "/tmp/NineChronicles/nekoyume/Assets/StreamingAssets/GoldDistribution.csv"
}
```

## 2. Build genesis block

There is `NineChronicles.Headless.Executable genesis` command to generate a genesis block.

```
cd /tmp/NineChronicles.Headless/NineChronicles.Headless.Executable
dotnet run -- genesis /tmp/config.json
```

After the execution, the command will leave genesis-block at `/tmp/NineChronicles.Headless/NineChronicles.Headless.Executable/genesis-block`

You can use the genesis block to organize your Nine Chronicles network.
