FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app
ARG COMMIT
ARG TARGETPLATFORM

# Copy csproj and restore as distinct layers
COPY ./Lib9c/Lib9c/Lib9c.csproj ./Lib9c/
COPY ./Libplanet.Headless/Libplanet.Headless.csproj ./Libplanet.Headless/
COPY ./NineChronicles.RPC.Shared/NineChronicles.RPC.Shared/NineChronicles.RPC.Shared.csproj ./NineChronicles.RPC.Shared/
COPY ./NineChronicles.Headless/NineChronicles.Headless.csproj ./NineChronicles.Headless/
COPY ./NineChronicles.Headless.Executable/NineChronicles.Headless.Executable.csproj ./NineChronicles.Headless.Executable/
RUN dotnet restore Lib9c
RUN dotnet restore Libplanet.Headless
RUN dotnet restore NineChronicles.RPC.Shared
RUN dotnet restore NineChronicles.Headless
RUN dotnet restore NineChronicles.Headless.Executable

# Copy everything else and build
COPY . ./
RUN <<EOF
#!/bin/bash
echo "TARGETPLATFROM=$TARGETPLATFORM"
if [[ "$TARGETPLATFORM" = "linux/amd64" ]]
then
  dotnet publish NineChronicles.Headless.Executable/NineChronicles.Headless.Executable.csproj \
    -c Release \
    -r linux-x64 \
    -o out \
    --self-contained \
    --version-suffix $COMMIT
elif [[ "$TARGETPLATFORM" = "linux/arm64" ]]
then
  dotnet publish NineChronicles.Headless.Executable/NineChronicles.Headless.Executable.csproj \
    -c Release \
    -r linux-arm64 \
    -o out \
    --self-contained \
    --version-suffix $COMMIT
else
  echo "Not supported target platform: '$TARGETPLATFORM'."
  exit -1
fi
EOF

# Build runtime image
FROM --platform=$TARGETPLATFORM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build-env /app/out .

# Install native deps & utilities for production
RUN apt-get update \
    && apt-get install -y --allow-unauthenticated \
        libc6-dev liblz4-dev zlib1g-dev libsnappy-dev libzstd-dev jq curl \
     && rm -rf /var/lib/apt/lists/*

VOLUME /data

ENTRYPOINT ["dotnet", "NineChronicles.Headless.Executable.dll"]
