# NineChronicles Standalone

## Run

```
$ dotnet run --project ./NineChronicles.Standalone.Executable/ -- --help

Usage: NineChronicles.Standalone.Executable [--no-miner] [--app-protocol-version <String>] \
    [--genesis-block-path <String>] [--host <String>] [--port <Nullable`1>] \
    [--minimum-difficulty <Int32>] [--private-key <String>] [--store-type <String>] \
    [--store-path <String>] [--ice-server <String>...] [--peer <String>...] \
    [--no-trusted-state-validators] [--trusted-app-protocol-version-signer <String>...] [--rpc-server] \
    [--rpc-listen-host <String>] [--rpc-listen-port <Nullable`1>] [--graphql-server] \
    [--graphql-host <String>] [--graphql-port <Nullable`1>] [--libplanet-node] [--no-mpt] \
    [--workers <Int32>] [--confirmations <Int32>] [--max-transactions <Int32>] [--strict-rendering] \
    [--dev] [--dev.block-interval <Int32>] [--dev.reorg-interval <Int32>] [--help] [--version]

Run standalone application with options.

Options:
  --no-miner
  -V, --app-protocol-version <String>                      App protocol version token (Default: )
  -G, --genesis-block-path <String>                         (Default: )
  -H, --host <String>                                       (Default: )
  -P, --port <Nullable`1>                                   (Default: )
  -D, --minimum-difficulty <Int32>                          (Default: 5000000)
  --private-key <String>                                    (Default: )
  --store-type <String>                                     (Default: )
  --store-path <String>                                     (Default: )
  -I, --ice-server <String>...                              (Default: )
  --peer <String>...                                        (Default: )
  --no-trusted-state-validators
  -T, --trusted-app-protocol-version-signer <String>...    Trustworthy signers who claim new app protocol versions (Default: )
  --rpc-server
  --rpc-listen-host <String>                                (Default: 0.0.0.0)
  --rpc-listen-port <Nullable`1>                            (Default: )
  --graphql-server
  --graphql-host <String>                                   (Default: 0.0.0.0)
  --graphql-port <Nullable`1>                               (Default: )
  --libplanet-node
  --no-mpt                                                 Flag to turn off the Merkle Patricia Trie for state saving.
  --workers <Int32>                                        Number of workers to use in Swarm (Default: 5)
  --confirmations <Int32>                                  The number of required confirmations to recognize a block.  0 by default. (Default: 0)
  --max-transactions <Int32>                               The number of maximum transactions can be included in a single block.
                                                           Unlimited if the value is less then or equal to 0.  100 by default. (Default: 100)
  --strict-rendering                                       Flag to turn on validating action renderer.
  --dev                                                    Flag to turn on the dev mode.  false by default.
  --dev.block-interval <Int32>                             The time interval between blocks. It's unit is milliseconds.
                                                           Works only when dev mode is on.  10000 (ms) by default. (Default: 10000)
  --dev.reorg-interval <Int32>                             The size of reorg interval. Works only when dev mode is on.  0 by default. (Default: 0)
  -h, --help                                               Show help message
  --version                                                Show version
```

## Docker Build

A Standalone image can be created by running the command below in the directory where the solution is located.

```
$ docker build . -t <IMAGE_TAG> --build-arg COMMIT=<VERSION_SUFFIX>
```

### Command Line Options

- `-H`, `--host`: Specifies the host name.
- `-P`, `--port`: Specifies the port number.
- `--private-key`: Specifies the private Key.
- `--no-miner`: Disables mining.
- `--no-trusted-state-validators`: Calculates all states directly without receiving calculated states from specified peers.
- `--store-path`: Specifies the path for storing data.
- `-I`, `--ice-server`: Specifies the TURN server info used for NAT Traversal. If there are multiple servers, they can be added by typing: `--ice-server serverA --ice-server serverB ...`.
- `--peer`: Adds a peer and if there are multiple peers, they can be added by typing: `--peer peerA --peer peerB ...`.
- `-G`, `--genesis-block-path`: Specifies the path of the genesis block.
- `-V`, `--app-protocol-version`: Specifies the value of `Swarm<T>.AppProtocolVersion`.
- `--rpc-server`: Starts with RPC server mode. Must specify `--rpc-listen-port` to use this mode.
- `--rpc-listen-host`: Host name for RPC server mode.
- `--rpc-listen-port`: Port number for RPC server mode.
-  `--graphql-server`: Turn on graphQL controller.
-  `--graphql-host`: Host name for graphQL controller.
-  `--graphql-port`: Port number for graphQL controller.
-  `--libplanet-node`: Run with formal Libplanet node. One of this or `graphql-server` must be set.
-  `--no-mpt`: Use legacy block state store instead of Merkle Patricia Trie for state saving.
-  `--workers`: Number of workers to use in Swarm.
-  `--confirmations`: Specifies the number of required confirmations to recognize a block.
-  `--max-transactions`: Specifies the number of maximum transactions can be included in a single block. Unlimited if the value is less then or equal to 0.
-  `--dev`: Flag to turn on the dev mode.
-  `--dev.block-interval`: Specifies the time interval between blocks by milliseconds in dev mode.
-  `--dev.reorg-interval`: Specifies the size of reorg interval in dev mode.

### Format

Formatting for `PrivateKey` or `Peer` follows the format in [Nekoyume Project README][../README.md].

## How to run NineChronicles Standalone on AWS EC2 instance using Docker

### A) On Your Local

#### Pre-requisites

- Docker environment: [Docker Installation Guide](https://docs.docker.com/get-started/#set-up-your-docker-environment)
- Docker hub account: [Docker Hub Guide](https://docs.docker.com/docker-hub/)

#### 1. Build Docker image with the tag name in [<DOCKER_HUB_ACCOUNT>/<IMAGE_NAME>] format.

```
$ docker build . --tag 9c/9c-standalone --build-arg COMMIT=9c-1

Usage: docker build . --tag [<DOCKER_HUB_ACCOUNT>/<IMAGE_NAME>] --build-arg COMMIT=[<VERSION_SUFFIX>]
```
- [Docker Build Guide](https://docs.docker.com/engine/reference/commandline/build/)

![Docker Build](https://i.imgur.com/vc6CnbR.png)

#### 2. Push your Docker image to your docker hub account.

```
$ docker push 9c/9c-standalone:latest

Usage: docker push [<DOCKER_HUB_ACCOUNT>/<IMAGE_NAME>] : [<VERSION>]
```
- [Docker Push Guide](https://docs.docker.com/engine/reference/commandline/push/)

![Docker Push](https://i.imgur.com/IRIXXjg.png)

### B) On Your AWS EC2 Instance

#### Pre-requisites

- Docker environment: [Docker Installation Guide](https://docs.docker.com/get-started/#set-up-your-docker-environment)
- Docker hub account: [Docker Hub Guide](https://docs.docker.com/docker-hub/)
- AWS EC2 instance: [AWS EC2 Guide](https://docs.aws.amazon.com/ec2/index.html)

#### 3. Pull your docker image to your AWS EC2 instance.

```
$ docker pull 9c/9c-standalone:latest

Usage: docker pull [<DOCKER_HUB_ACCOUNT>/<IMAGE_NAME>] : [<VERSION>]
```
- [Docker Pull Guide](https://docs.docker.com/engine/reference/commandline/pull/)

![Docker Pull](https://i.imgur.com/e7sSKxH.png)

#### 4. Create a docker volume for blockchain data persistance

```
$ docker volume create 9c-volume
Usage: docker volume create [<VOLUME_NAME>]
```
- [Docker Volume Guide](https://docs.docker.com/engine/reference/commandline/volume_create/)

![Docker Volume Create](https://i.imgur.com/ISgKeLc.png)

#### 5. Run your docker image with your docker volume mounted (use -d for detached mode)

```
$ docker run \
--detach \
--volume 9c-volume:/app/data \
9c/9c-standalone \
--app-protocol-version=1000005/019101FEec7ed4f918D396827E1277DEda1e20D4/MEUCIQCdvof4eiiLRm187vEEh.C8fbJNKuqF47EJSZeymWA5pgIgU.+Jbm2g6tUdchIgoWDZ6Xw1HwSTi1GFz9Vcxt9I0p0= \
--genesis-block-path=https://9c-test.s3.ap-northeast-2.amazonaws.com/genesis-block-9c-beta-9-rc1 \
--minimum-difficulty=5000000 \
--store-type=rocksdb \
--store-path=/app/data \
--ice-server=turn://0ed3e48007413e7c2e638f13ddd75ad272c6c507e081bd76a75e4b7adc86c9af:0apejou+ycZFfwtREeXFKdfLj2gCclKzz5ZJ49Cmy6I=@turn-us.planetarium.dev:3478 \
--ice-server=turn://0ed3e48007413e7c2e638f13ddd75ad272c6c507e081bd76a75e4b7adc86c9af:0apejou+ycZFfwtREeXFKdfLj2gCclKzz5ZJ49Cmy6I=@turn-us2.planetarium.dev:3478 \
--ice-server=turn://0ed3e48007413e7c2e638f13ddd75ad272c6c507e081bd76a75e4b7adc86c9af:0apejou+ycZFfwtREeXFKdfLj2gCclKzz5ZJ49Cmy6I=@turn-us3.planetarium.dev:3478 \
--peer=027bd36895d68681290e570692ad3736750ceaab37be402442ffb203967f98f7b6,9c-internal.planetarium.dev,31236 \
--trusted-app-protocol-version-signer=02a5e2811a9bfa4eec274e806debd622c53702bce39a809918563a4cf34189ff85 \
--no-trusted-state-validators=true \
--workers=20 \
--confirmations=2 \
--libplanet-node
```

![Docker Run](https://i.imgur.com/bPlUqF1.png)

- [Docker volumes usage](https://docs.docker.com/storage/volumes/)
- [NineChronicles Standalone Docker run usage](#run)
