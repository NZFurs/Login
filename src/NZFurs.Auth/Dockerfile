FROM microsoft/dotnet:2.2-aspnetcore-runtime AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM microsoft/dotnet:2.2-sdk AS build
WORKDIR /src
COPY ["NZFurs.Auth/NZFurs.Auth.csproj", "NZFurs.Auth/"]
RUN dotnet restore "NZFurs.Auth/NZFurs.Auth.csproj"
COPY . .
WORKDIR "/src/NZFurs.Auth"
RUN dotnet build "NZFurs.Auth.csproj" -c Release -o /app

FROM build AS publish
RUN dotnet publish "NZFurs.Auth.csproj" -c Release -o /app

FROM base AS final
WORKDIR /app
ENV DOTNET_CLI_TELEMETRY_OPTOUT 1
ENV DOTNET_SKIP_FIRST_TIME_EXPERIENCE 1
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "NZFurs.Auth.dll"]