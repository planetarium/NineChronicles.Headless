# NineChronicles Headless

## Table of Contents

- [Run](#run)
- [Docker Build](#docker-build)
  * [Command Line Options](#command-line-options)
  * [Format](#format)
- [How to run NineChronicles Headless on AWS EC2 instance using Docker](#how-to-run-ninechronicles-headless-on-aws-ec2-instance-using-docker)
  * [On Your AWS EC2 Instance](#on-your-aws-ec2-instance)
  * [Building Your Own Docker Image from Your Local](#building-your-own-docker-image-from-your-local)
- [Nine Chronicles GraphQL API Documentation](#nine-chronicles-graphql-api-documentation)
- [Create New Genesis Block](#create-new-genesis-block)

## Run

```
$ dotnet run --project ./NineChronicles.Headless.Executable/ -- --help

Usage: NineChronicles.Headless.Executable [command]
Usage: NineChronicles.Headless.Executable [--app-protocol-version <String>] [--genesis-block-path <String>] [--host <String>] [--port <UInt16>] [--swarm-private-key <String>] [--workers <Int32>] [--no-miner] [--miner-count <Int32>] [--miner-private-key <String>] [--store-type <String>] [--store-path <String>] [--no-reduce-store] [--ice-server <String>...] [--peer <String>...] [--trusted-app-protocol-version-signer <String>...] [--rpc-server] [--rpc-listen-host <String>] [--rpc-listen-port <Int32>] [--rpc-remote-server] [--rpc-http-server] [--graphql-server] [--graphql-host <String>] [--graphql-port <Int32>] [--graphql-secret-token-path <String>] [--no-cors] [--confirmations <Int32>] [--nonblock-renderer] [--nonblock-renderer-queue <Int32>] [--strict-rendering] [--log-action-renders] [--network-type <NetworkType>] [--dev] [--dev.block-interval <Int32>] [--dev.reorg-interval <Int32>] [--aws-cognito-identity <String>] [--aws-access-key <String>] [--aws-secret-key <String>] [--aws-region <String>] [--tx-life-time <Int32>] [--message-timeout <Int32>] [--tip-timeout <Int32>] [--demand-buffer <Int32>] [--static-peer <String>...] [--skip-preload] [--minimum-broadcast-target <Int32>] [--bucket-size <Int32>] [--chain-tip-stale-behavior-type <String>] [--tx-quota-per-signer <Int32>] [--maximum-poll-peers <Int32>] [--help] [--version]

NineChronicles.Headless.Executable

Commands:
  validation
  chain
  key
  apv
  action
  tx
  genesis

Options:
  -V, --app-protocol-version <String>                      App protocol version token. (Required)
  -G, --genesis-block-path <String>                        Genesis block path of blockchain. Blockchain is recognized by its genesis block. (Required)
  -H, --host <String>                                      Hostname of this node for another nodes to access. This is not listening host like 0.0.0.0
  -P, --port <UInt16>                                      Port of this node for another nodes to access.
  --swarm-private-key <String>                             The private key used for signing messages and to specify your node. If you leave this null, a randomly generated value will be used.
  --workers <Int32>                                        Number of workers to use in Swarm (Default: 5)
  --no-miner                                               Disable block mining.
  --miner-count <Int32>                                    The number of miner task(thread). (Default: 1)
  --miner-private-key <String>                             The private key used for mining blocks. Must not be null if you want to turn on mining with libplanet-node.
  --store-type <String>                                    The type of storage to store blockchain data. If not provided, "LiteDB" will be used as default. Available type: ["rocksdb", "memory"]
  --store-path <String>                                    Path of storage. This value is required if you use persistent storage e.g. "rocksdb"
  --no-reduce-store                                        Do not reduce storage. Enabling this option will use enormous disk spaces.
  -I, --ice-server <String>...                             ICE server to NAT traverse.
  --peer <String>...                                       Seed peer list to communicate to another nodes.
  -T, --trusted-app-protocol-version-signer <String>...    Trustworthy signers who claim new app protocol versions
  --rpc-server                                             Run RPC server?
  --rpc-listen-host <String>                               RPC listen host (Default: 0.0.0.0)
  --rpc-listen-port <Int32>                                RPC listen port
  --rpc-remote-server                                      Do a role as RPC remote server? If you enable this option, multiple Unity clients can connect to your RPC server.
  --rpc-http-server                                        If you enable this option with "rpcRemoteServer" option at the same time, RPC server will use HTTP/1, not gRPC.
  --graphql-server                                         Run GraphQL server?
  --graphql-host <String>                                  GraphQL listen host (Default: 0.0.0.0)
  --graphql-port <Int32>                                   GraphQL listen port
  --graphql-secret-token-path <String>                     The path to write GraphQL secret token. If you want to protect this headless application, you should use this option and take it into headers.
  --no-cors                                                Run without CORS policy.
  --confirmations <Int32>                                  The number of required confirmations to recognize a block. (Default: 0)
  --nonblock-renderer                                      Uses non-blocking renderer, which prevents the blockchain & swarm from waiting slow rendering.  Turned off by default.
  --nonblock-renderer-queue <Int32>                        The size of the queue used by the non-blocking renderer. Ignored if --nonblock-renderer is turned off. (Default: 512)
  --strict-rendering                                       Flag to turn on validating action renderer.
  --log-action-renders                                     Log action renders besides block renders.  --rpc-server implies this.
  --network-type <NetworkType>                             Network type. (Default: Main) (Allowed values: Main, Internal, Permanent, Test, Default)
  --dev                                                    Flag to turn on the dev mode.  false by default.
  --dev.block-interval <Int32>                             The time interval between blocks. It's unit is milliseconds. Works only when dev mode is on. (Default: 10000)
  --dev.reorg-interval <Int32>                             The size of reorg interval. Works only when dev mode is on. (Default: 0)
  --aws-cognito-identity <String>                          The Cognito identity for AWS CloudWatch logging.
  --aws-access-key <String>                                The access key for AWS CloudWatch logging.
  --aws-secret-key <String>                                The secret key for AWS CloudWatch logging.
  --aws-region <String>                                    The AWS region for AWS CloudWatch (e.g., us-east-1, ap-northeast-2).
  --tx-life-time <Int32>                                   The lifetime of each transaction, which uses minute as its unit. (Default: 180)
  --message-timeout <Int32>                                The grace period for new messages, which uses second as its unit. (Default: 60)
  --tip-timeout <Int32>                                    The grace period for tip update, which uses second as its unit. (Default: 60)
  --demand-buffer <Int32>                                  A number of block size that determines how far behind the demand the tip of the chain will publish `NodeException` to GraphQL subscriptions. (Default: 1150)
  --static-peer <String>...                                A list of peers that the node will continue to maintain.
  --skip-preload                                           Run node without preloading.
  --minimum-broadcast-target <Int32>                       Minimum number of peers to broadcast message. (Default: 10)
  --bucket-size <Int32>                                    Number of the peers can be stored in each bucket. (Default: 16)
  --chain-tip-stale-behavior-type <String>                 Determines behavior when the chain's tip is stale. "reboot" and "preload" is available and "reboot" option is selected by default. (Default: reboot)
  --tx-quota-per-signer <Int32>                            The number of maximum transactions can be included in stage per signer. (Default: 10)
  --maximum-poll-peers <Int32>                             The maximum number of peers to poll blocks. int.MaxValue by default. (Default: 2147483647)
  -h, --help                                               Show help message
  --version                                                Show version
```

## Docker Build

A headless image can be created by running the command below in the directory where the solution is located.

```
$ docker build . -t <IMAGE_TAG> --build-arg COMMIT=<VERSION_SUFFIX>
```
* Nine Chronicles Team uses <VERSION_SUFFIX> to build an image with the latest git commit and push to the [official Docker Hub repository](https://hub.docker.com/repository/docker/planetariumhq/ninechronicles-headless). However, if you want to build and push to your own Docker Hub account, <VERSION_SUFFIX> can be any value.

### Command Line Options

- `-H`, `--host`: Specifies the host name.
- `-P`, `--port`: Specifies the port number.
- `--swarm-private-key`: Specifies the private Key used in swarm.
- `--miner-private-key`: Specifies the private Key used in mining.
- `--no-miner`: Disables mining.
- `--store-path`: Specifies the path for storing data.
- `-I`, `--ice-server`: Specifies the TURN server info used for NAT Traversal. If there are multiple servers, they can be added by typing: `--ice-server serverA --ice-server serverB ...`.
- `--peer`: Adds a peer and if there are multiple peers, they can be added by typing: `--peer peerA --peer peerB ...`.
- `-G`, `--genesis-block-path`: Specifies the path of the genesis block.
- `-V`, `--app-protocol-version`: Specifies the value of `Swarm<T>.AppProtocolVersion`.
- `--rpc-server`: Starts with RPC server mode. Must specify `--rpc-listen-port` to use this mode.
- `--rpc-listen-host`: Host name for RPC server mode.
- `--rpc-listen-port`: Port number for RPC server mode.
- `--graphql-server`: Turn on graphQL controller.
- `--graphql-host`: Host name for graphQL controller.
- `--graphql-port`: Port number for graphQL controller.
- `--libplanet-node`: Run with formal Libplanet node. One of this or `graphql-server` must be set.
- `--workers`: Number of workers to use in Swarm.
- `--confirmations`: Specifies the number of required confirmations to recognize a block.
- `--nonblock-renderer`: Uses non-blocking renderer, which prevents the blockchain & swarm from waiting slow rendering.  Turned off by default.
- `--nonblock-renderer-queue`: The size of the queue used by the non-blocking renderer.  512 by default.  Ignored if `--nonblock-renderer` is turned off.
- `--max-transactions`: Specifies the number of maximum transactions can be included in a single block. Unlimited if the value is less then or equal to 0.
- `--network-type`: Choose one of `Main`, `Internal`, `Test`.  `Main` by defualt.
- `--dev`: Flag to turn on the dev mode.
- `--dev.block-interval`: Specifies the time interval between blocks by milliseconds in dev mode.
- `--dev.reorg-interval`: Specifies the size of reorg interval in dev mode.
- `--message-timeout`: Specifies the time limit that determines how old the latest message is received will publish `NodeException` to GraphQL subscriptions.
- `--tip-timeout`: Specifies the time limit that determines how old the blockchain's tip is updated will publish `NodeException` to GraphQL subscriptions.
- `--demand-buffer`: Specifies the number that determines how far behind the demand the tip of the chain will publish `NodeException` to GraphQL subscriptions.
- `--miner-count`: Specifies the number of task(thread)s to use for mining.
- `--skip-preload`: Skipping preloading when starting.
- `--minimum-broadcast-target`: Minimum number of the peers to broadcast messages.
- `--bucket-size`: Specifies the number of the peers can be stored in each bucket.
- `--tx-quota-per-signer`: Specifies the number of maximum transactions can be included in stage per signer.
- `--poll-interval`: Specifies the interval between block polling.
- `--maximum-poll-peers`: Specifies the maximum number of peers to poll blocks.

### Format

Formatting for `PrivateKey` or `Peer` follows the format in [Nekoyume Project README][../README.md].

## How to run NineChronicles Headless on AWS EC2 instance using Docker

### On Your AWS EC2 Instance

#### Pre-requisites

- Docker environment: [Docker Installation Guide](https://docs.docker.com/get-started/#set-up-your-docker-environment)
- AWS EC2 instance: [AWS EC2 Guide](https://docs.aws.amazon.com/ec2/index.html)

#### 1. Pull ninechronicles-headless Docker image to your AWS EC2 instance from the [official Docker Hub repository](https://hub.docker.com/repository/docker/planetariumhq/ninechronicles-headless).

* If you would like to build your own Docker image from your local, refer to [this section](#building-your-own-docker-image-from-your-local).

```
$ docker pull planetariumhq/ninechronicles-headless:latest

Usage: docker pull [<DOCKER_HUB_ACCOUNT>/<IMAGE_NAME>] : [<TAGNAME>]
```
- [Docker Pull Guide](https://docs.docker.com/engine/reference/commandline/pull/)

![Docker Pull](https://i.imgur.com/oLCULZr.png)

#### 2. Create a Docker volume for blockchain data persistance

```
$ docker volume create 9c-volume
Usage: docker volume create [<VOLUME_NAME>]
```
- [Docker Volume Guide](https://docs.docker.com/engine/reference/commandline/volume_create/)

![Docker Volume Create](https://i.imgur.com/ISgKeLc.png)

#### 3. Run your Docker image with your Docker volume mounted (use -d for detached mode)

<pre>
$ docker run \
--detach \
--volume 9c-volume:/app/data \
planetariumhq/ninechronicles-headless:latest \
<a href = "#run" title="NineChronicles Headless options">[NineChronicles Headless Options]</a>
</pre>
#### Note)

* If you want to use the same headless options as your Nine Chronicles game client, refer to **`config.json`** under **`%localappdata%\Programs\Nine Chronicles\resources\app`**. Inside **`config.json`**, refer to the following properties for your headless options:
  - `GeniesisBlockPath`
  - `MinimumDifficulty`
  - `StoreType`
  - `AppProtocolVersion`
  - `TrustedAppProtocolVersionSigners`
  - `IceServerStrings`
  - `PeerStrings`
  - `NoTrustedStateValidators`
  - `NoMiner`
  - `Confirmations`
  - `NonblockRenderer`
  - `NonblockRendererQueue`
  - `Workers`
* If you are using an [Elastic IP](https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/elastic-ip-addresses-eip.html) on your AWS instance, you must include the IP as the `--host` option but do not need to include the `--ice-server` option.
* For mining, make sure to include the `--miner-private-key` option with your private key. Also, include `--libplanet-node` to run the default libplanet node.

![Docker Run](https://i.imgur.com/VlwFybj.png)

- [Docker Volumes Usage](https://docs.docker.com/storage/volumes/)

### Building Your Own Docker Image from Your Local

#### Pre-requisites

- Docker environment: [Docker Installation Guide](https://docs.docker.com/get-started/#set-up-your-docker-environment)
- Docker Hub account: [Docker Hub Guide](https://docs.docker.com/docker-hub/)

#### 1. Build Docker image with the tag name in [<DOCKER_HUB_ACCOUNT>/<IMAGE_NAME>] format.

```
$ docker build . --tag 9c/9c-headless --build-arg COMMIT=9c-1

Usage: docker build . --tag [<DOCKER_HUB_ACCOUNT>/<IMAGE_NAME>] : [<TAGNAME>] --build-arg COMMIT=[<VERSION_SUFFIX>]
```
- [Docker Build Guide](https://docs.docker.com/engine/reference/commandline/build/)

![Docker Build](https://i.imgur.com/iz74t3J.png)

#### 2. Push your Docker image to your Docker Hub account.

```
$ docker push 9c/9c-headless:latest

Usage: docker push [<DOCKER_HUB_ACCOUNT>/<IMAGE_NAME>] : [<TAGNAME>]
```
- [Docker Push Guide](https://docs.docker.com/engine/reference/commandline/push/)

![Docker Push](https://i.imgur.com/NWUW9LS.png)

## Nine Chronicles GraphQL API Documentation

Check out [Nine Chronicles GraphQL API Tutorial](https://www.notion.so/Getting-Started-with-Nine-Chronicles-GraphQL-API-a14388a910844a93ab8dc0a2fe269f06) to get you started with using GraphQL API with NineChronicles Headless.

For more information on the GraphQL API, refer to the [NineChronicles Headless GraphQL Documentation](http://api.nine-chronicles.com/).

---

## Create new genesis block

### 1. (Optional) Create activation keys and PendingActivationState
Activation key is the code for 9c account to register/activate into NineChronicles.  
You can create activation key whenever you want later, so you can just skip this step.

```shell
dotnet run --project ./.Lib9c.Tools tx create-activation-keys 10 > ActivationKeys.csv  # Change [10] to your number of new activation keys
dotnet run --project ./.Lib9c.Tools tx create-pending-activations ActivationKeys.csv > PendingActivation
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
| extra                                      |                     | Optional | Extra setting.                                                                                                                                                                     |
| extra.pendingActivationStatePath           | string              |          | If you want to set activation key inside genesis block to use, create `PendingActivationData` and save to file and provide here.                                                   |

### 3. Create genesis block
```shell
dotnet run --project ./NineChronicles.Headless.Executable/ genesis ./config.json
```
After this step, you will get `genesis-block` file as output and another info in addition.

### 4. Run Headless node with genesis block
```shell
dotnet run --project ./NineChronicles.Headless.Executable/ \
    -V=100260/6ec8E598962F1f475504F82fD5bF3410eAE58B9B/MEUCIQCG2yQNyXu3ovuUBNMEQiqx1vdo.FCMet9FoayFiIL89QIgXGRTU84nrcmLL4ud2j9ogrGt7ScmqaD97N.4rrtraXE=/ZHUxNjpXaW5kb3dzQmluYXJ5VXJsdTU2Omh0dHBzOi8vZG93bmxvYWQubmluZS1jaHJvbmljbGVzLmNvbS92MTAwMjYwL1dpbmRvd3MuemlwdTk6dGltZXN0YW1wdTEwOjIwMjItMDctMjhl \ 
    -G=[PATH/TO/GENESIS/BLOCK] \
    --store-type=memory \
    --store-path= [PATH/TO/BLOCK/STORAGE] \
    --workers=1000 \
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
