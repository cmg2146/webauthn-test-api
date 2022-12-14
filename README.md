# WebAuthn Test API
This repo contains the API for the WebAuthn-Test application. See the `webauthn-test-web` repo for a
complete description of the application.

This web API is a simple <span>ASP.</span>NET Core 6.0 app which communicates with a SQL Server database using
Entity Framework Core.

The app was created with the following dotnet CLI command:

 ```dotnet new webapi --exclude-launch-settings --framework net6.0 --use-program-main```

## Build
In development, the API can be run using Docker Linux containers by executing the following command at the repo root:

```docker-compose up```

### Configuration
The following environment variables must be configured, at run time, for proper operation:

* ASPNETCORE_ENVIRONMENT
  * "Development" or "Production"
* WEB_URL
  * The HTTPS URL to the client/front-end app, i.e. https://localhost:10000
* SQLCONNSTR_DEFAULT
  * The connection string to the SQL Server database
* AZURE_KEY_VAULT_ID (Production Only)
  * The Azure Key vault identifier. Key Vault encrpyts data protection keys at rest.

For development, all environment variables have already been set in the docker compose file and can
be tweaked as needed. Some other environment variables, not listed above, are required for development and
have also been set in the docker-compose file.

## Database Migrations
Migrations must be added whenever making schema changes to the database. To add a migration, run the following command
at the repo root:

```dotnet ef migrations add NameOfNewMigration --project ./src/database --startup-project ./src/api -- ConnectionStrings:Default="Data Source=Dummy String"```

Due to the way the EF Core CLI tool acquires the DbContext, a connection string must be passed to any command or the
command will fail. Fortunately, the add migration command doesn't need a database, so a bogus string can be used.
Other commands might fail without a valid connection string. See the [EF Core Tools](https://learn.microsoft.com/en-us/ef/core/cli/dotnet).

The app has been configured to update the database automatically (apply all pending migrations) at startup.

## Notes

To run any dotnet CLI command, version 6.0.x of the .NET SDK must be installed, which can be downloaded
[here](https://dotnet.microsoft.com/en-us/download/dotnet/6.0).

The following documentation was helpful to setup this project:

[Docker for <span>ASP.</span>NET Core](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/docker/building-net-docker-images?view=aspnetcore-6.0)

[Configuring SQL Server for Docker](https://learn.microsoft.com/en-us/sql/linux/sql-server-linux-docker-container-configure)

[Sync Container Startup in Docker Compose](https://github.com/vishnubob/wait-for-it)


* TODO: Migrations applied at startup/runtime will not work with multiple instances
* TODO: Setup <span>ASP.</span>NET core debugging w/docker
