# Dockerfile based off samples in https://github.com/dotnet/dotnet-docker-samples

# "Build Stage" Container: "build-env"
FROM microsoft/dotnet:2.2-sdk AS build-env

ENV DOTNET_CLI_TELEMETRY_OPTOUT 1
ENV DOTNET_SKIP_FIRST_TIME_EXPERIENCE 1

WORKDIR /app

# copy csproj and restore as distinct layers
COPY *.sln .
COPY src/NZFurs.Auth/*.csproj ./src/NZFurs.Auth/

RUN dotnet restore

# copy everything else and publish
COPY src/ ./src/

RUN dotnet publish -c Release -o out

# "Runtime Stage" Container: "runtime"
FROM microsoft/dotnet:2.2-aspnetcore-runtime AS runtime

WORKDIR /app

ENV DOTNET_CLI_TELEMETRY_OPTOUT 1
ENV DOTNET_SKIP_FIRST_TIME_EXPERIENCE 1
EXPOSE 80
EXPOSE 443

COPY --from=build-env /app/src/NZFurs.Auth/out ./

ENTRYPOINT ["dotnet", "NZFurs.Auth.dll"]
