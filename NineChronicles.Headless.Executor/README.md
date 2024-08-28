### NineChronicles.Headless.Executor

```
# NineChronicles.Headless.Executor

This project provides a command-line interface (CLI) to manage and execute different versions of NineChronicles Headless nodes. You can install, list, and run the headless node for specific networks such as `MainnetOdin`, `MainnetHeimdall`, or `Single`.

## Prerequisites

- .NET SDK 6.0 or later
- Internet connection for downloading headless versions and necessary configuration files

## Installation

Clone the repository and navigate to the project folder:

```bash
git clone <repository_url>
cd NineChronicles.Headless.Executor
```

## Build and Run

You can build and run the project using the following commands:

```bash
dotnet build
dotnet run -- <command> [options]
```

## Commands

### Install

Install a specific version of NineChronicles Headless:

```bash
dotnet run -- install <version> [--os <platform>]
```

- `<version>`: The version of NineChronicles.Headless to download.
- `--os`: Optionally specify the OS platform (auto-detected if not provided).

### List Versions

List installed versions or remote versions available for download:

```bash
dotnet run -- versions [--remote] [--page <page_number>]
```

- `--remote`: If provided, lists remote versions available for download.
- `--page`: Specifies the page number for pagination of remote versions.

### Run

Run a specific version of NineChronicles Headless on a given network:

```bash
dotnet run -- run <version> <planet>
```

- `<version>`: The version of NineChronicles.Headless to run.
- `<planet>`: The network to run the headless node on (e.g., `MainnetOdin`, `MainnetHeimdall`, `Single`).

The `run` command automatically downloads the necessary configuration files (e.g., templates, genesis block) if they are not already present locally.

### Example

To run version `v100000` of NineChronicles Headless on the `MainnetOdin` network:

```bash
dotnet run -- run v100000 MainnetOdin
```

## Configuration

The project automatically downloads the necessary configuration files, such as templates and genesis blocks, when running the headless node. These files are saved in user directories for reuse.

- Configuration templates are stored in `~/.planetarium/headless/templates/`.
- Genesis blocks are stored in `~/.planetarium/headless/genesis-block/`.
