# NineChronicles Standalone

## Run

```
$ dotnet run --project ./NineChronicles.Standalone.Executable/ -- --help

Usage: NineChronicles.Standalone.Executable [--app-protocol-version <Int32>] [--genesis-block-path <String>] [--no-miner] [--host <String>] [--port <Nullable`1>] [--private-key <String>] [--store-type <String>] [--store-path <String>] [--ice-server <String>...] [--peer <String>...] [--rpc-server] [--rpc-listen-host <String>] [--rpc-listen-port <Nullable`1>] [--help] [--version]

Run standalone application with options.

Options:
  -V, --app-protocol-version <Int32>     (Required)
  -G, --genesis-block-path <String>      (Required)
  --no-miner
  --no-trusted-state-validators
  -H, --host <String>                    (Default: )
  -P, --port <Nullable`1>                (Default: )
  --private-key <String>                 (Default: )
  --store-type <String>                  (Default: )
  --store-path <String>                  (Default: )
  -I, --ice-server <String>...           (Default: )
  --peer <String>...                     (Default: )
  --rpc-server                          
  --rpc-listen-host <String>             (Default: 0.0.0.0)
  --rpc-listen-port <Nullable`1>         (Default: )
  -h, --help                            Show help message
  --version                             Show version
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

### Format

Formatting for `PrivateKey` or `Peer` follows the format in [Nekoyume Project README][../README.md].
