FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build-env
WORKDIR /app
ARG COMMIT

# Copy csproj and restore as distinct layers
COPY ./Lib9c/Lib9c/Lib9c.csproj ./Lib9c/
COPY ./Libplanet.Standalone/Libplanet.Standalone.csproj ./Libplanet.Standalone/
COPY ./NineChronicles.RPC.Shared/NineChronicles.RPC.Shared/NineChronicles.RPC.Shared.csproj ./NineChronicles.RPC.Shared/
COPY ./NineChronicles.Standalone/NineChronicles.Standalone.csproj ./NineChronicles.Standalone/
COPY ./NineChronicles.Standalone.Executable/NineChronicles.Standalone.Executable.csproj ./NineChronicles.Standalone.Executable/
RUN dotnet restore Lib9c
RUN dotnet restore Libplanet.Standalone
RUN dotnet restore NineChronicles.RPC.Shared
RUN dotnet restore NineChronicles.Standalone
RUN dotnet restore NineChronicles.Standalone.Executable

# Copy everything else and build
COPY . ./
RUN dotnet publish NineChronicles.Standalone.Executable/NineChronicles.Standalone.Executable.csproj \
    -c Release \
    -r linux-x64 \
    -o out \
    --self-contained \
    --version-suffix $COMMIT

# Build runtime image
FROM mcr.microsoft.com/dotnet/core/aspnet:3.1
WORKDIR /app
RUN apt-get update && apt-get install -y libc6-dev
COPY --from=build-env /app/out .

VOLUME /data

ENTRYPOINT ["dotnet", "NineChronicles.Standalone.Executable.dll", "--host", "0.0.0.0", "--port", "31234"]
