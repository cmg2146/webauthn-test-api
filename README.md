# WebAuthn Test API
This repo contains the API for the WebAuthn-Test application. See the
[webauthn-test-web](https://github.com/cmg2146/webauthn-test-web) repo for a complete description of the
application.

This web API is a simple <span>ASP.</span>NET Core 6.0 app which communicates with a SQL Server database using
Entity Framework Core.

The app was created with the following dotnet CLI command:

 ```dotnet new webapi --exclude-launch-settings --framework net6.0 --use-program-main```

## Build
In development, the API can be run using Docker Linux containers by executing the following command at the repo root:

```docker-compose up```

In production, an Azure DevOps pipeline (azure-pipelines.yml) automatically runs when changes are made to the main branch.
The pipeline requires the following variables to be configured to properly deploy the updated code:

* AZURE_SERVICE_CONNECTION
  * The name of the service connection to Azure. A service connection must be created in Azure DevOps
  for the pipeline to communicate with Azure.
* CONTAINER_REGISTRY_SERVICE_CONNECTION
  * The name of the service connection to the Azure Container Registry (ACR). Docker images are pushed to this ACR.
  A service connection must be created in Azure DevOps for the pipeline to communicate with the ACR.
* CONTAINER_REGISTRY_NAMESPACE
  * The host name of the container registry, for example "{your acr name}.azurecr.io"
* CONTAINER_IMAGE_REPOSITORY
  * The name of the Docker image, for example "webauthn-test/api"
* APP_NAME
  * The name of the Azure App Service that hosts the web API.

Currently, the variables above are set in a variable group in Azure DevOps.

### Debug
To debug in VS Code, open the Debug tab and start the "Docker Attach" launch config. This will attach the debugger to
the running container. Make sure to select "Yes" when prompted to copy the debugger to the container.

### Configuration
The following run-time environment variables must be configured for proper operation:

* ASPNETCORE_ENVIRONMENT
  * "Development" or "Production"
* WEB_URL
  * The HTTPS URL to the client/frontend app, i.e. https://localhost:10000
* SQLCONNSTR_DEFAULT
  * The connection string to the SQL Server database
* KEY_VAULT_DATAPROTECTION_KEY_ID (Production Only)
  * The URI of the Key Vault key used to encrypt ASP.NET Core Data Protection Keys.

For development, all environment variables have already been set in the docker compose file and can
be tweaked as needed. Some other environment variables, not listed above, are required for development and
have also been set in the docker-compose file.

In production, the variables are set in a variable group in Azure DevOps.

## Database Migrations
Migrations must be added whenever making schema changes to the database. To add a migration, run the following command
at the repo root:

```dotnet ef migrations add NameOfNewMigration --project ./src/database --startup-project ./src/api -- ConnectionStrings:Default="Data Source=Dummy String"```

Due to the way the EF Core CLI tool acquires the DbContext, a connection string must be passed to any command or the
command will fail. Fortunately, the add migration command doesn't need a database, so a bogus string can be used.
Other commands might fail without a valid connection string. See the [EF Core Tools](https://learn.microsoft.com/en-us/ef/core/cli/dotnet).

For development only, the app has been configured to update the database automatically (apply all pending migrations) at startup.
For production, the database can be migrated using a one-off process by running the following command in the app's execution environment:

`dotnet WebAuthnTest.Api.dll --migrate-database`

## Notes

To run any dotnet CLI command, version 6.0.x of the .NET SDK must be installed, which can be downloaded
[here](https://dotnet.microsoft.com/en-us/download/dotnet/6.0).

The following documentation was helpful to setup this project:

[Docker for <span>ASP.</span>NET Core](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/docker/building-net-docker-images?view=aspnetcore-6.0)

[Configuring SQL Server for Docker](https://learn.microsoft.com/en-us/sql/linux/sql-server-linux-docker-container-configure)
