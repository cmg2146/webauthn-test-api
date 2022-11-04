# WebAuthn Test Application 
This application contains a very basic sample of passwordless and usernameless authentication with WebAuthn.

## Structure
The application contains three parts:

1. database
2. api
3. ui

The database part is a .NET 6.0 class library, relying on EF Core to communicate with the actual database,
and the api part is a simple ASP.NET Core 6.0 app. The ASP.NET Core app references the database class library
and builds into a single executable, comprising the API or "backend" portion of the application.

The ui part is implemented as a static web site using Vue.js and Nuxt. In production, the UI is served by the
ASP.NET Core app, leveraging ASP.NET Core's static file serving feature. This is accomplished simply by copying
the Vue.js built static files to the "wwwrooot" folder in the ASP.NET Core app.

Serving the UI and API on the same server makes it easy to use cookies for an authentication session and
use a client side framework like Vue.

The ASP.NET Core app was created with the following dotnet CLI command:

 ```dotnet new webapi --exclude-launch-settings --framework net6.0 --use-program-main```

## Build
In development, the solution can be run using Docker by executing the following command at the repo root:

```docker-compose up```

...and then opening your browser to http://localhost:10000.

The app should automatically redirect you to HTTPS. If your browser warns you the site is unsafe, you can
either "proceed as unsafe" or add the development certificate to your certificate store to avoid the warning
again.

Note that the file name of the development certificate must match the name of the ASP.NET Core app assembly,
i.e. WebAuthnTest.Api.pfx. This development certificate must not be used in production! It was created using
the `dotnet dev-certs https` CLI command.

The following environment variables must be configured for proper operation:

ASPNETCORE_ENVIRONMENT
APP_URL
SQLCONNSTR_DEFAULT
SQLAZURECONNSTR_DEFAULT (Production Only)
AZURE_KEY_VAULT_ID (Production Only)

For development, the environment variables have already been set in the docker compose file and can
be tweaked as needed. Some other environment variables, not listed above, are required for development and
have also been set in the docker-compose file.



## EF Core Migrations
You will need to add migrations whenever making schema changes to the database. To add a migration, run the following command
within the "api" project:

```dotnet ef migrations add NameOfNewMigration --project ../database -- ConnectionStrings:Default="Data Source=Dummy String"```

Due to the way the EF Core CLI tool acquires the DbContext, you will need to pass a connection string to any command or the
command will fail. Fortunately, the add migration command doesn't really need a database, so you can pass a bogus string.
Other commands might fail without a valid connection string. See the [EF Core Tools](https://learn.microsoft.com/en-us/ef/core/cli/dotnet).

The app has been configured to update the database automatically (apply all pending migrations) at startup.

## Notes

To run any dotnet CLI command, you will need version 6.0.x of the dotnet SDK installed on your machine.

TODO: Setup Vue app
TODO: Setup https wih Vue
TODO: Setup asp.net core debugging w/docker
TODO: Setup ARM template, possibly with build pipeline


The following documentation was helpful to setup this project:

[Docker for ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/docker/building-net-docker-images?view=aspnetcore-6.0)

[Host ASP.NET Core with HTTPS in Docker](https://github.com/dotnet/dotnet-docker/blob/main/samples/host-aspnetcore-https.md)

[Configuring SQL Server for Docker](https://learn.microsoft.com/en-us/sql/linux/sql-server-linux-docker-container-configure)

[Sync Container Startup in Docker Compose](https://github.com/vishnubob/wait-for-it)