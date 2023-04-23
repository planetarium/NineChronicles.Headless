# NineChronicles Headless

[![Planetarium Discord invite](https://img.shields.io/discord/539405872346955788?color=6278DA&label=Planetarium&logo=discord&logoColor=white)](https://discord.gg/JyujU8E4SD)
[![Planetarium-Dev Discord Invite](https://img.shields.io/discord/928926944937013338?color=6278DA&label=Planetarium-dev&logo=discord&logoColor=white)](https://discord.gg/RYJDyFRYY7)

## Table of Contents

- [Run](#run)
- [Docker Build](#docker-build)
  * [Format](#format)
- [How to run NineChronicles Headless on AWS EC2 instance using Docker](#how-to-run-ninechronicles-headless-on-aws-ec2-instance-using-docker)
  * [On Your AWS EC2 Instance](#on-your-aws-ec2-instance)
  * [Building Your Own Docker Image from Your Local Machine](#building-your-own-docker-image-from-your-local-machine)
- [Nine Chronicles GraphQL API Documentation](#nine-chronicles-graphql-api-documentation)
- [Create A New Genesis Block](#create-a-new-genesis-block)

## Run

```
$ dotnet run --project ./NineChronicles.Headless.Executable/ -- --help

Usage: NineChronicles.Headless.Executable [command]
Usage: NineChronicles.Headless.Executable 

// basic
[--app-protocol-version <String>]
[--trusted-app-protocol-version-signer <String>...]
[--genesis-block-path <String>] 
[--host <String>]
[--port <UInt16>] 
[--swarm-private-key <String>]
  
// Policy
[--skip-preload] 
[--chain-tip-stale-behavior-type <String>] 
[--confirmations <Int32>]
[--tx-life-time <Int32>] 
[--message-timeout <Int32>] 
[--tip-timeout <Int32>] 
[--demand-buffer <Int32>]
[--tx-quota-per-signer <Int32>]
[--maximum-poll-peers <Int32>]

// Store
[--store-type <String>] 
[--store-path <String>]
[--no-reduce-store]
 
// Network
[--network-type <NetworkType>]
[--ice-server <String>...] 
[--peer <String>...] 
[--static-peer <String>...]
[--minimum-broadcast-target <Int32>] 
[--bucket-size <Int32>] 
 
// render
[--nonblock-renderer]
[--nonblock-renderer-queue <Int32>] 
[--strict-rendering]
[--log-action-renders]

// consensus
[--consensus-port <UInt16>] 
[--consensus-private-key <String>] 
[--consensus-seed <String>...] 

// RPC
[--rpc-server]
[--rpc-listen-host <String>] 
[--rpc-listen-port <Int32>] 
[--rpc-remote-server]
[--rpc-http-server]

// GraphQL
[--graphql-server] 
[--graphql-host <String>]
[--graphql-port <Int32>]
[--graphql-secret-token-path <String>]
[--no-cors]
   
// Sentry
[--sentry-dsn <String>] 
[--sentry-trace-sample-rate <Double>]

// ETC
[--config <String>] 
[--help]
[--version]

// Miner (Deprecated)
[--no-miner] 
[--miner-count <Int32>] 
[--miner-private-key <String>] 
[--miner.block-interval <Int32>]


NineChronicles.Headless.Executable

Commands:
  account
  validation
  chain
  key
  apv
  action
  state
  tx
  market
  genesis
  replay

Options:
  -V, --app-protocol-version <String>                      App protocol version token.
  -G, --genesis-block-path <String>                        Genesis block path of blockchain. Blockchain is recognized by its genesis block.
  -H, --host <String>                                      Hostname of this node for another nodes to access. This is not listening host like 0.0.0.0
  -P, --port <UInt16>                                      Port of this node for another nodes to access.
  --swarm-private-key <String>                             The private key used for signing messages and to specify your node. If you leave this null, a randomly generated value will be used.
  --no-miner                                               Disable block mining.
  --miner-count <Int32>                                    The number of miner task(thread).
  --miner-private-key <String>                             The private key used for mining blocks. Must not be null if you want to turn on mining with libplanet-node.
  --miner.block-interval <Int32>                           The miner's break time after mining a block. The unit is millisecond.
  --store-type <String>                                    The type of storage to store blockchain data. If not provided, "LiteDB" will be used as default. Available type: ["rocksdb", "memory"]
  --store-path <String>                                    Path of storage. This value is required if you use persistent storage e.g. "rocksdb"
  --no-reduce-store                                        Do not reduce storage. Enabling this option will use enormous disk spaces.
  -I, --ice-server <String>...                             ICE server to NAT traverse.
  --peer <String>...                                       Seed peer list to communicate to another nodes.
  -T, --trusted-app-protocol-version-signer <String>...    Trustworthy signers who claim new app protocol versions
  --rpc-server                                             Use this option if you want to make unity clients to communicate with this server with RPC
  --rpc-listen-host <String>                               RPC listen host
  --rpc-listen-port <Int32>                                RPC listen port
  --rpc-remote-server                                      Do a role as RPC remote server? If you enable this option, multiple Unity clients can connect to your RPC server.
  --rpc-http-server                                        If you enable this option with "rpcRemoteServer" option at the same time, RPC server will use HTTP/1, not gRPC.
  --graphql-server                                         Use this option if you want to enable GraphQL server to enable querying data.
  --graphql-host <String>                                  GraphQL listen host
  --graphql-port <Int32>                                   GraphQL listen port
  --graphql-secret-token-path <String>                     The path to write GraphQL secret token. If you want to protect this headless application, you should use this option and take it into headers.
  --no-cors                                                Run without CORS policy.
  --confirmations <Int32>                                  The number of required confirmations to recognize a block.
  --nonblock-renderer                                      Uses non-blocking renderer, which prevents the blockchain & swarm from waiting slow rendering. Turned off by default.
  --nonblock-renderer-queue <Int32>                        The size of the queue used by the non-blocking renderer. Ignored if --nonblock-renderer is turned off.
  --strict-rendering                                       Flag to turn on validating action renderer.
  --log-action-renders                                     Log action renders besides block renders. --rpc-server implies this.
  --network-type <NetworkType>                             Network type. (Allowed values: Main, Internal, Permanent, Test, Default)
  --tx-life-time <Int32>                                   The lifetime of each transaction, which uses minute as its unit.
  --message-timeout <Int32>                                The grace period for new messages, which uses second as its unit.
  --tip-timeout <Int32>                                    The grace period for tip update, which uses second as its unit.
  --demand-buffer <Int32>                                  A number of block size that determines how far behind the demand the tip of the chain will publish `NodeException` to GraphQL subscriptions.
  --static-peer <String>...                                A list of peers that the node will continue to maintain.
  --skip-preload                                           Run node without preloading.
  --minimum-broadcast-target <Int32>                       Minimum number of peers to broadcast message.
  --bucket-size <Int32>                                    Number of the peers can be stored in each bucket.
  --chain-tip-stale-behavior-type <String>                 Determines behavior when the chain's tip is stale. "reboot" and "preload" is available and "reboot" option is selected by default.
  --tx-quota-per-signer <Int32>                            The number of maximum transactions can be included in stage per signer.
  --maximum-poll-peers <Int32>                             The maximum number of peers to poll blocks. int.MaxValue by default.
  --consensus-port <UInt16>                                Port used for communicating consensus related messages.  null by default.
  --consensus-private-key <String>                         The private key used for signing consensus messages. Cannot be null.
  --consensus-seed <String>...                             A list of seed peers to join the block consensus.
  -C, --config <String>                                    Absolute path of "appsettings.json" file to provide headless configurations. (Default: appsettings.json)
  --sentry-dsn <String>                                    Sentry DSN
  --sentry-trace-sample-rate <Double>                      Trace sample rate for sentry
  -h, --help                                               Show help message
  --version                                                Show version
```

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

For more information on the GraphQL API, refer to the [NineChronicles Headless GraphQL Documentation](http://api.nine-chronicles.com/).

---

## Create a new genesis block

### 1. (Optional) Create activation keys and PendingActivationState
Activation key is the code for 9c account to register/activate into NineChronicles.
You can create activation key whenever you want later, so you can just skip this step.

```shell
dotnet run --project NineChronicles.Headless.Executable/NineChronicles.Headless.Executable.csproj -- tx create-activation-keys 10 > ActivationKeys.csv  # Change [10] to your number of new activation keys
dotnet run --project NineChronicles.Headless.Executable/NineChronicles.Headless.Executable.csproj -- tx create-pending-activations ActivationKeys.csv > PendingActivation
```

### 2. Create config file for genesis block
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
| admin                                      |                     | Optional | Related to admin setting.                                                                                                                                                          |
| admin.activate                             | bool                |          | If true, give admin privilege to admin address.                                                                                                                                    |
| admin.address                              | Address (string)    |          | Address to be admin. If not provided, the `initialMinter` will be set as admin.                                                                                                    |
| admin.validUntil                           | long                |          | Block number of admin lifetime. Admin address loses its privilege after this block.                                                                                                |
| initialValidatorSet                        |                     | Optional | Initial Validator set for this blockchain. Do not provide this section if you want to use default setting.                                                                         |   
| initialValidatorSet[i].publicKey           | PublicKey (string)  |          | Public Key of validator.                                                                                                                                                           |
| initialValidatorSet[i].power               | long                |          | Voting power of validator. Min. value of voting power is 1.                                                                                                                        |
| extra                                      |                     | Optional | Extra settings.                                                                                                                                                                    |
| extra.pendingActivationStatePath           | string              |          | If you want to set activation key inside genesis block to use, create `PendingActivationData` and save to file and provide here.                                                   |

### 3. Create genesis block

```shell
dotnet run --project ./NineChronicles.Headless.Executable/ genesis ./config.json
```

After this step, you will get `genesis-block` file as output and another info in addition.

### 4. Run Headless node with genesis block

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
