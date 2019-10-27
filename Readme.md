# NZFurs Login Server ![License](https://img.shields.io/github/license/NZFurs/Login.svg)

## Contribute ![GitHub last commit](https://img.shields.io/github/last-commit/nzfurs/login.svg) ![GitHub issues](https://img.shields.io/github/issues/nzfurs/login.svg)

### Code of Conduct [![Contributor Covenant](https://img.shields.io/badge/Contributor%20Covenant-v1.4%20adopted-ff69b4.svg)](code-of-conduct.md)

Please note that this project is released with a [Contributor Code of Conduct](CODE_OF_CONDUCT.md). By participating in this project you agree to abide by its terms.

### Setting up a development environment:

1. Install the lastest [.NET Core SDK](https://dotnet.microsoft.com/download)
2. `git clone https://github.com/NZFurs/Login.git`
3. Initialise the SQLite database:
    * `dotnet ef database update --project src/NZFurs.Auth/ --context ApplicationDbContext`
    * `dotnet ef database update --project src/NZFurs.Auth/ --context ConfigurationDbContext`
    * `dotnet ef database update --project src/NZFurs.Auth/ --context PersistedGrantDbContext`
4. Connect to Azure (JWT signing operations via Azure Key Vault)
    * Install the latest [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli)
    * `az login`
    * Either:
        * You have been given access to the Development Key Vault, in which case you don't need to do anything
        * Create a key vault in Azure, add a valid RSA 2048 key to it, then update `Azure:KeyVault` in `appsettings.Development.json` to point to it

## Build [![Build Status](https://dev.azure.com/nzfurs/Login/_apis/build/status/develop)](https://dev.azure.com/nzfurs/Login/_build/latest?definitionId=2)

### Build using Docker

1. Ensure you have the latest .NET Core SDK and Docker installed
2. Run the following Docker command:

```bash
docker build . -t nzfurs/login:development
```

## Docker ![Docker Pulls](https://img.shields.io/docker/pulls/nzfurs/login.svg)

To run a server locally:

1. Add your development environment Azure AD credentials to `development.env` (a sample is provided). If you don't have these details, either ask @tcfox or set up your own in Azure (details on what's required coming soon).
2. Run the following Docker command (either Bash or Powershell will work). If you wish to run your own build, replace the tag (`latest`) with one you provided in the "Build" instructions above (such as `development`).

```bash
docker run -it \
-p 80:80 -p 443:443 \
--env-file development.env \
--volume=${PWD}/app/database:/app/data/database \
--volume=${PWD}/app/logs:/app/data/logs \
nzfurs/login:latest
```

## Server ![Production](https://img.shields.io/website-up-down-green-red/https/login.furry.nz.svg?label=Production) ![Development](https://img.shields.io/website-up-down-green-red/https/dev.login.furry.nz.svg?label=Development)