# Dockerfile based off samples in https://github.com/dotnet/dotnet-docker-samples

# "Build Stage" Container: "build-env"
FROM mcr.microsoft.com/dotnet/core/sdk:2.2 AS build-env

ENV DOTNET_CLI_TELEMETRY_OPTOUT 1
ENV DOTNET_SKIP_FIRST_TIME_EXPERIENCE 1

WORKDIR /app

# copy csproj and restore as distinct layers
COPY *.sln .
COPY src/NZFurs.Auth/*.csproj ./src/NZFurs.Auth/

RUN dotnet restore ./src/NZFurs.Auth/

# copy everything else and publish
COPY src/ ./src/

RUN dotnet publish -c Release -o out src/NZFurs.Auth/

# "Runtime Stage" Container: "runtime"
FROM mcr.microsoft.com/dotnet/core/aspnet:2.2 AS runtime

WORKDIR /app

ENV DOTNET_CLI_TELEMETRY_OPTOUT 1
ENV DOTNET_SKIP_FIRST_TIME_EXPERIENCE 1
EXPOSE 80
EXPOSE 443

COPY --from=build-env /app/src/NZFurs.Auth/out ./

ENTRYPOINT ["dotnet", "NZFurs.Auth.dll"]
