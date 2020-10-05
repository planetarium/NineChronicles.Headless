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
    [--graphql-host <String>] [--graphql-port <Nullable`1>] [--libplanet-node] [--mpt] \
    [--workers <Int32>] [--confirmations <Int32>] [--dev] [--dev.block-interval <Int32>]
    [--dev.reorg-interval <Int32>] [--help] [--version]

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
  --mpt                                                    Flag to turn on the Merkle trie feature. It is experimental.
  --workers <Int32>                                        Number of workers to use in Swarm (Default: 5)
  --confirmations <Int32>                                  The number of required confirmations to recognize a block.  0 by default. (Default: 0)
  --dev                                                    Flag to turn on the dev mode.  false by default.
  --dev.block-interval <Int32>                             The time interval between blocks. It's unit is seconds. Works only when dev mode is on.  10 (s) by default. (Default: 10)
  --dev.reorg-interval <Int32>                             The size of reorg interval. Works only when dev mode is on.  0 by default. (Default: 0)
  -h, --help                                               Show help message
  --version                                                Show version
```

## Docker Build

A Standalone image can be created by running the command below in the directory where the solution is located.

```
$ docker build . -t <IMAGE_TAG>
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
-  `--mpt`: Use the Merkle trie based storage.
-  `--workers`: Number of workers to use in Swarm.
-  `--confirmations`: Specifies the number of required confirmations to recognize a block.
-  `--dev`: Flag to turn on the dev mode.
-  `--dev.block-interval`: Specifies the time interval between blocks by seconds in dev mode.
-  `--dev.reorg-interval`: Specifies the size of reorg interval in dev mode.

### Format

Formatting for `PrivateKey` or `Peer` follows the format in [Nekoyume Project README][../README.md].
