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

The following environment variables must be configured for proper operation:

ASPNETCORE_ENVIRONMENT
SQLCONNSTR_DEFAULT
SQLAZURECONNSTR_DEFAULT (Production)
AZURE_KEY_VAULT_ID (Production)
APP_URL

For development, the environment variables have already been populated in the docker compose file, but can
be tweaked as needed.


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

TODO: Setup docker
TODO: Setup https
TODO: Setup debugging
TODO: Setup ARM template, possibly with build pipeline