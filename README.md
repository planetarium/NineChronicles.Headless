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

## Run

```
$ dotnet run --project ./NineChronicles.Headless.Executable/ -- --help
Usage: NineChronicles.Headless.Executable [command]
Usage: NineChronicles.Headless.Executable [--app-protocol-version <String>] [--genesis-block-path <String>] [--no-miner] [--host <String>] [--port <Nullable`1>] [--swarm-private-key <String>] [--minimum-difficulty <Int32>] [--miner-private-key <String>] [--store-type <String>] [--store-path <String>] [--ice-server <String>...] [--peer <String>...] [--trusted-app-protocol-version-signer <String>...] [--rpc-server] [--rpc-listen-host <String>] [--rpc-listen-port <Nullable`1>] [--graphql-server] [--graphql-host <String>] [--graphql-port <Nullable`1>] [--graphql-secret-token-path <String>] [--no-cors] [--workers <Int32>] [--confirmations <Int32>] [--max-transactions <Int32>] [--strict-rendering] [--dev] [--dev.block-interval <Int32>] [--dev.reorg-interval <Int32>] [--log-action-renders] [--aws-cognito-identity <String>] [--aws-access-key <String>] [--aws-secret-key <String>] [--aws-region <String>] [--authorized-miner] [--tx-life-time <Int32>] [--message-timeout <Int32>] [--tip-timeout <Int32>] [--demand-buffer <Int32>] [--static-peer <String>...] [--miner-count <Int32>] [--skip-preload] [--minimum-broadcast-target <Int32>] [--bucket-size <Int32>] [--chain-tip-stale-behavior-type <String>] [--poll-interval <Int32>] [--maximum-poll-peers <Int32>] [--completion] [--help] [--version]

NineChronicles.Headless.Executable

Commands:
  validation
  chain
  key

Options:
  -V, --app-protocol-version <String>                      App protocol version token (Required)
  -G, --genesis-block-path <String>                         (Required)
  --no-miner
  -H, --host <String>
  -P, --port <Nullable`1>
  --swarm-private-key <String>                             The private key used for signing messages and to specify your node. If you leave this null, a randomly generated value will be used.
  -D, --minimum-difficulty <Int32>                          (Default: 5000000)
  --miner-private-key <String>                             The private key used for mining blocks. Must not be null if you want to turn on mining with libplanet-node.
  --store-type <String>
  --store-path <String>
  -I, --ice-server <String>...
  --peer <String>...
  -T, --trusted-app-protocol-version-signer <String>...    Trustworthy signers who claim new app protocol versions
  --rpc-server
  --rpc-listen-host <String>                                (Default: 0.0.0.0)
  --rpc-listen-port <Nullable`1>
  --graphql-server
  --graphql-host <String>                                   (Default: 0.0.0.0)
  --graphql-port <Nullable`1>
  --graphql-secret-token-path <String>                     The path to write GraphQL secret token. If you want to protect this headless application, you should use this option and take it into headers.
  --no-cors                                                Run without CORS policy.
  --workers <Int32>                                        Number of workers to use in Swarm (Default: 5)
  --confirmations <Int32>                                  The number of required confirmations to recognize a block.  0 by default. (Default: 0)
  --max-transactions <Int32>                               The number of maximum transactions can be included in a single block. Unlimited if the value is less then or equal to 0.  100 by default. (Default: 100)
  --strict-rendering                                       Flag to turn on validating action renderer.
  --dev                                                    Flag to turn on the dev mode.  false by default.
  --dev.block-interval <Int32>                             The time interval between blocks. It's unit is milliseconds. Works only when dev mode is on.  10000 (ms) by default. (Default: 10000)
  --dev.reorg-interval <Int32>                             The size of reorg interval. Works only when dev mode is on.  0 by default. (Default: 0)
  --log-action-renders                                     Log action renders besides block renders.  --rpc-server implies this.
  --aws-cognito-identity <String>                          The Cognito identity for AWS CloudWatch logging.
  --aws-access-key <String>                                The access key for AWS CloudWatch logging.
  --aws-secret-key <String>                                The secret key for AWS CloudWatch logging.
  --aws-region <String>                                    The AWS region for AWS CloudWatch (e.g., us-east-1, ap-northeast-2).
  --authorized-miner                                       Run as an authorized miner, which mines only blocks that should be authorized.
  --tx-life-time <Int32>                                   The lifetime of each transaction, which uses minute as its unit.  60 (m) by default. (Default: 60)
  --message-timeout <Int32>                                The grace period for new messages, which uses second as its unit.  60 (s) by default. (Default: 60)
  --tip-timeout <Int32>                                    The grace period for tip update, which uses second as its unit.  60 (s) by default. (Default: 60)
  --demand-buffer <Int32>                                  A number that determines how far behind the demand the tip of the chain will publish `NodeException` to GraphQL subscriptions.  1150 blocks by default. (Default: 1150)
  --static-peer <String>...                                A list of peers that the node will continue to maintain.
  --miner-count <Int32>                                    The number of miner task(thread). (Default: 1)
  --skip-preload                                           Run node without preloading.
  --minimum-broadcast-target <Int32>                       Minimum number of peers to broadcast message.  10 by default. (Default: 10)
  --bucket-size <Int32>                                    Number of the peers can be stored in each bucket.  16 by default. (Default: 16)
  --chain-tip-stale-behavior-type <String>                 Determines behavior when the chain's tip is stale. "reboot" and "preload" is available and "reboot" option is selected by default. (Default: reboot)
  --poll-interval <Int32>                                  The interval between block polling.  15 seconds by default. (Default: 15)
  --maximum-poll-peers <Int32>                             The maximum number of peers to poll blocks.  int.MaxValue by default. (Default: 2147483647)
  --completion                                             Generate a shell completion code
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
- `--max-transactions`: Specifies the number of maximum transactions can be included in a single block. Unlimited if the value is less then or equal to 0.
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
