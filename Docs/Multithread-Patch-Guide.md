# Multithread Patch Guide

## Setup
1. Install Visual Code
1. Install Visual Code Plugin (C#): https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp
1. Install .NET 5.0 SDK: https://dotnet.microsoft.com/download/dotnet/thank-you/sdk-5.0.301-windows-x64-installer?journey=vs-code
1. Install .NET 3.1 SDK: https://dotnet.microsoft.com/download/dotnet/thank-you/sdk-3.1.410-windows-x64-installer
1. I highly recommend just restarting your pc after this, as I had to for the libraries to be recognized

## Preparing Source Code
(YOU MUST BE AUTHENTICATED WITH GITHUB TO DOWNLOAD SUB-MODULES)
1. Download HEADLESS source code from here: 
    - git clone https://github.com/planetarium/NineChronicles.Headless
1. Checkout proper tag for current version:
    - git checkout ebbcd7dc7e341b4856e717de9bf90d6ce34d0c08
1. Initiate Sub-modules:
    - git submodule update --recursive --init
1. Apply Swen's patch for LibPlanet. This is to prevent 0 TX's
    - cd Lib9c/.Libplanet && git checkout 425320bc5d8140a9817a56988b144510416bef72 && cd ../..

## Making Code Modifications
1. Open file located at: NineChronicles.Headless/NineChroniclesNodeService.cs
1. Goto line (20) and add this line: "using System.Linq;" under "using System;"
```C#
using Serilog.Events;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
```

1. Goto line (172) and change this:

```
Log.Debug("Start mining.");
await miner.MineBlockAsync(properties.MaximumTransactions, cancellationToken);
```
to
```
Log.Debug("Start mining.");

int[] ids = new[] { 1, 2, 3, 4 };   //Number of threads
await Task.WhenAll(ids.Select(i => miner.MineBlockAsync(properties.MaximumTransactions, cancellationToken)));
```

## Building with Docker
1. docker build . -t <USER>/<REPO>:<VERSION> --build-arg COMMIT=$COMMIT_TAG

## Building on Windows 10
1. I was presented with a popup in the lower right corner to install missing dependencies. If you werent go ahead and run the build script and see if it prompts you then. (Located at: scripts\buildHeadless.cmd)

```
Scripts
    - buildHeadless.cmd     // Builds Win64 HEADLESS
    - packageHeadless.cmd   // Builds Win64 HEADLESS and packages it into a zip with a start HEADLESS script
    - startHeadless.cmd     // Start HEADLESS script
```