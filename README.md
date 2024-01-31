# NineChronicles Headless

[![Planetarium Discord invite](https://img.shields.io/discord/539405872346955788?color=6278DA&label=Planetarium&logo=discord&logoColor=white)](https://discord.gg/JyujU8E4SD)
[![Planetarium-Dev Discord Invite](https://img.shields.io/discord/928926944937013338?color=6278DA&label=Planetarium-dev&logo=discord&logoColor=white)](https://discord.gg/RYJDyFRYY7)
[![Discourse posts](https://img.shields.io/discourse/posts?server=https%3A%2F%2Fdevforum.nine-chronicles.com%2F&logo=discourse&label=9c-devforum&color=00D1C2
)](https://devforum.nine-chronicles.com)

## Run

If you want to run node to interact with mainnet, you can run the below command line:

```
dotnet run --project NineChronicles.Headless.Executable -C appsettings.mainnet.json --store-path={PATH_TO_STORE}
```

For more information on the command line options, refer to the [CLI Documentation](https://planetarium.github.io/NineChronicles.Headless/cli).

### Use `appsettings.{network}.json` to provide CLI options

You can provide headless CLI options using file, `appsettings.json`. You'll find the default file at [here](NineChronicles.Headless.Executable/appsettings.json).
The path of `appsettings.json` can be either local file storage or URL.
Refer full configuration fields from [this file](NineChronicles.Headless.Executable/Configuration.cs), set your options into `appsettings.json` under `Headless` section.
You can also run headless server with previous way; You don't need to change anything if you don't want to.
In case that the same option is provided from both `appsetting.json` and CLI option, the CLI option value is used instead from `appsettings.json`.

The default `appsettings.json` is an example for your own appsettings file.
`appsettings.{network}.json` are runnable appsettings file and you can run local node for each network using following command:
```shell
dotnet run --project NineChronicles.Headless.Executable -C appsettings.{network}.json --store-type={YOUR_OWN_STORE_PATH}
```
- appsettings.mainnet.json
  - This makes your node to connect to the Nine Chronicles mainnet (production).
- appsettings.internal.json
  - This makes your node to connect to the Nine Chronicles internal network, which is test before release new version.
  - Internal network is kind of hard-fork of mainnet at some point to test new version.
  - You CANNOT use mainnet storage for internal headless node.
- appsettings.previewnet.json
  - This makes your node to connect to the Nine Chronicles preview network to show feature preview.
  - This network is totally different network from genesis block.
  - Previewnet can be restarted from genesis block without any announcement to prepare next feature.

Please make sure the store in the path you provided must save right data for the network to connect.
You cannot share data from any of those networks.

If you want to run your own isolated local network, please copy `appsettings.json` to `appsettings.local.json` and edit the contents.
```shell
cp appsettings.json appsettings.local.json
# Edit contents of appsettings.local.json
dotnet run --project NineChronicles.Headless.Executable -C appsettings.local.json --store-type={YOUR_OWN_STORE_PATH}
```

#### Caveat
APVs can be changed as Nine Chronicles deploys new version.
You have to fit your APV sting to current on-chain version string.
You can get APV strings at the following places:
- mainnet: [Official released config.json](https://release.nine-chronicles.com/9c-launcher-config.json) - `AppProtocolVersion`
- internal: [Internal network config](https://github.com/planetarium/9c-k8s-config/blob/main/9c-internal/configmap-versions.yaml) - `APP_PROTOCOL_VERSION`
- previewnet: [Previewnet config](https://github.com/planetarium/9c-k8s-config/blob/main/9c-previewnet/configmap-versions.yaml) - `APP_PROTOCOL_VERSION`

## Docker Build

A headless image can be created by running the command below in the directory where the solution is located.

```
$ docker build . -t <IMAGE_TAG> --build-arg COMMIT=<VERSION_SUFFIX>
```
* Nine Chronicles Team uses `<VERSION_SUFFIX>` to build an image with the latest git commit and push to the [official Docker Hub repository](https://hub.docker.com/repository/docker/planetariumhq/ninechronicles-headless). However, if you want to build and push to your own Docker Hub account, the `<VERSION_SUFFIX>` can be any value.

### Format

Formatting for `PrivateKey` or `Peer` follows the format in [Nekoyume Project README][../README.md].

## How to run NineChronicles Headless on AWS EC2 instance using Docker

### On Your AWS EC2 Instance

#### Pre-requisites

- Docker environment: [Docker Installation Guide](https://docs.docker.com/get-started/#set-up-your-docker-environment)
- AWS EC2 instance: [AWS EC2 Guide](https://docs.aws.amazon.com/ec2/index.html)

#### 1. Pull `planetariumhq/ninechronicles-headless` Docker image to your AWS EC2 instance from the [official Docker Hub repository](https://hub.docker.com/repository/docker/planetariumhq/ninechronicles-headless).

- If you would like to build your own Docker image from your local machine, refer to [this section](#building-your-own-docker-image-from-your-local-machine).

```
$ docker pull planetariumhq/ninechronicles-headless:[<TAGNAME>] (ex: v100300)
```
- Please refer to the `docker` value in https://release.nine-chronicles.com/apv.json for the latest official Docker tag name.
- [Docker Pull Guide](https://docs.docker.com/engine/reference/commandline/pull/)

#### 2. Create a Docker volume for blockchain data persistence

```
$ docker volume create [<VOLUME_NAME>] (ex: 9c-volume)
```
- [Docker Volume Guide](https://docs.docker.com/engine/reference/commandline/volume_create/)

#### 3. Run your Docker image with your Docker volume mounted (use -d for detached mode)

<pre>
$ docker run \
--detach \
--volume 9c-volume:/app/data \
planetariumhq/ninechronicles-headless:[<TAGNAME>] \
<a href = "#run" title="NineChronicles Headless options">[NineChronicles Headless Options]</a>
</pre>
#### Note)

- If you want to use the same headless options as your Nine Chronicles game client, please refer to the `headlessArgs` values in value in https://release.nine-chronicles.com/apv.json.
- If you are using an [Elastic IP](https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/elastic-ip-addresses-eip.html) on your AWS instance, you can add the IP address in the `--host` option and not use the `--ice-server` option.
- For mining, make sure to include the `--miner-private-key` option with your private key.

- [Docker Volumes Usage](https://docs.docker.com/storage/volumes/)

### Building Your Own Docker Image from Your Local Machine

#### Pre-requisites

- Docker environment: [Docker Installation Guide](https://docs.docker.com/get-started/#set-up-your-docker-environment)
- Docker Hub account: [Docker Hub Guide](https://docs.docker.com/docker-hub/)

#### 1. Build Docker image with the tag name in `[<DOCKER_HUB_ACCOUNT>/<IMAGE_NAME>]` format.

```
$ docker build . --tag [<DOCKER_HUB_ACCOUNT>/<IMAGE_NAME>]:[<TAGNAME>] --build-arg COMMIT=[<VERSION_SUFFIX>]
```
- [Docker Build Guide](https://docs.docker.com/engine/reference/commandline/build/)

#### 2. Push your Docker image to your Docker Hub account.

```
$ docker push [<DOCKER_HUB_ACCOUNT>/<IMAGE_NAME>]:[<TAGNAME>]
```
- [Docker Push Guide](https://docs.docker.com/engine/reference/commandline/push/)

## Nine Chronicles GraphQL API Documentation

Check out [Nine Chronicles GraphQL API Tutorial](https://www.notion.so/Getting-Started-with-Nine-Chronicles-GraphQL-API-a14388a910844a93ab8dc0a2fe269f06) to get you started with using GraphQL API with NineChronicles Headless.

For more information on the GraphQL API, refer to the [NineChronicles Headless GraphQL Documentation](https://planetarium.github.io/NineChronicles.Headless/graphql).

---

## Create a new genesis block

### 1. Create config file for genesis block
1. Copy `config.json.example` to `config.json`
2. Change values inside `config.json`
   - `data.tablePath` is required.
   - If you have `PendingActivation` file, set file path to `extra.pendingActivationStatePath`

#### Structure of genesis block
| Key                                        | Type                | Required | Description                                                                                                                                                                        |
|:-------------------------------------------|---------------------|:--------:|:-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| data                                       |                     | Required | Related to the game data. This field is required.                                                                                                                                  |
| data.tablePath                             | string              |          | Path of game data table. `Lib9c/Lib9c/TableCSV` for NineChronicles.                                                                                                                |
| currency                                   |                     | Optional | Related to currency data. Set initial mint / deposits.                                                                                                                             |
| currency.initialMinter                     | PrivateKey (string) |          | PrivateKey of initial minter.  <br/>Initial minting Tx. will be signed using this key and included in genesis block.                                                               |
| currency.initialCurrencyDeposit            | List                |          | Initial deposit data. These data will be created to Tx. and goes inside of genesis block.  <br/>If you leave this to null, the `initialMinter` will get 10000 currency as default. |
| currency.initialCurrencyDeposit[i].address | Address (string)    |          | Address of depositor. Use address string except leading `0x`.                                                                                                                      |
| currency.initialCurrencyDeposit[i].amount  | BigInteger          |          | Amount of currency give to depositor. <br/>This amount will be given every block from start to end. ex) 100 from 0 to 9: total 1000 currency will be given.                        |
| currency.initialCurrencyDeposit[i].start   | long                |          | First block to give currency to depositor. genesis block is #0                                                                                                                     |
| currency.initialCurrencyDeposit[i].end     | long                |          | Last block to give currency to depositor. <br/>If you want to give only once, set this value as same as `start`.                                                                   |
| currency.allowMint                         | boolean             |          | Allow/Disallow additional mint or burn against the initial currency.                                                                                                               |
| admin                                      |                     | Optional | Related to admin setting.                                                                                                                                                          |
| admin.activate                             | bool                |          | If true, give admin privilege to admin address.                                                                                                                                    |
| admin.address                              | Address (string)    |          | Address to be admin. If not provided, the `initialMinter` will be set as admin.                                                                                                    |
| admin.validUntil                           | long                |          | Block number of admin lifetime. Admin address loses its privilege after this block.                                                                                                |
| initialValidatorSet                        |                     | Optional | Initial Validator set for this blockchain. Do not provide this section if you want to use default setting.                                                                         |   
| initialValidatorSet[i].publicKey           | PublicKey (string)  |          | Public Key of validator.                                                                                                                                                           |
| initialValidatorSet[i].power               | long                |          | Voting power of validator. Min. value of voting power is 1.                                                                                                                        |
| initialMeadConfigs                         |                     | Optional | Initial MEAD distributions                                                                                                                                                         |
| initialMeadConfigs[i].address              | Address (string)    |          | Recipient address                                                                                                                                                                  |
| initialMeadConfigs[i].amount               | BigInteger          |          | Amount of initial MEAD                                                                                                                                                             |
| initialPledgeConfigs                       |                     | Optional | Initial pledges introduced from NCIP-15                                                                                                                                            |
| initialPledgeConfigs[i].agentAddress       | Address (string)    |          | Address of agent who will be funded                                                                                                                                                |
| initialPledgeConfigs[i].patronAddress      | Address (string)    |          | Address of patron who will fund                                                                                                                                                    |
| initialPledgeConfigs[i].mead               | int                 |          | Amount of MEAD that will be funded per each blocks                                                                                                                                 |

### 2. Create genesis block

```shell
dotnet run --project ./NineChronicles.Headless.Executable/ genesis ./config.json
```

After this step, you will get `genesis-block` file as output and another info in addition.

### 3. Run Headless node with genesis block

```shell
dotnet run --project ./NineChronicles.Headless.Executable/ \
    -V=[APP PROTOCOL VERSION] \
    -G=[PATH/TO/GENESIS/BLOCK] \
    --store-type=memory \
    --store-path= [PATH/TO/BLOCK/STORAGE] \
    --host=localhost \
    --port=43210 \
    --miner-private-key=[PRIVATE_KEY_OF_BLOCK_MINER]
```
If you see log like this, all process is successfully done:

```text
Start mining.
[BlockChain] 424037645/18484: Starting to mine block #1 with difficulty 5000000 and previous hash 29f53d22...
[BlockChain] Gathering transactions to mine for block #1 from 0 staged transactions...
[BlockChain] Gathered total of 0 transactions to mine for block #1 from 0 staged transactions.
Evaluating actions in the block #1 pre-evaluation hash: 10d93de7...
Evaluating policy block action for block #1 System.Collections.Immutable.ImmutableArray`1[System.Byte]
Actions in 0 transactions for block #1 pre-evaluation hash: 10d93de7... evaluated in 20ms.
[BlockChain] 424037645/18484: Mined block #1 0838b084... with difficulty 5000000 and previous hash 29f53d22...
[BlockChain] Trying to append block #1 0838b084...
[BlockChain] Unstaging 0 transactions from block #1 0838b084...
[BlockChain] Unstaged 0 transactions from block #1 0838b084...
[Swarm] Trying to broadcast blocks...
[NetMQTransport] Broadcasting message Libplanet.Net.Messages.BlockHeaderMessage as 0x7862DD9b....Unspecified/localhost:43210. to 0 peers
[Swarm] Block broadcasting complete.
[BlockChain] Appended the block #1 0838b084...
[BlockChain] Invoking renderers for #1 0838b084... (1 renderer(s), 0 action renderer(s))
[LoggedRenderer] Invoking RenderBlock() for #1 0838b084... (was #0 29f53d22...)...
[LoggedRenderer] Invoked RenderBlock() for #1 0838b084... (was #0 29f53d22...).
[BlockChain] Invoked renderers for #1 0838b084... (1 renderer(s), 0 action renderer(s))
[Swarm] Trying to broadcast blocks...
[NetMQTransport] Broadcasting message Libplanet.Net.Messages.BlockHeaderMessage as 0x7862DD9b....Unspecified/localhost:43210. to 0 peers
[Swarm] Block broadcasting complete.
```
