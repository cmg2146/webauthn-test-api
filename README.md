# WebAuthn Test Application 
This application contains a very basic sample of passwordless and usernameless authentication with WebAuthn.

## Structure
The application contains two parts:

1. server (API with database access via ORM)
2. client (front-end/UI)

The server part is a simple <span>ASP.</span>NET Core 6.0 web API. The API communicates with the database using
Entity Framework Core.

The front end is implemented as a static web site using Vue.js and Nuxt.

In production and in development, the web API and front end are hosted separately, but requests to the API are
proxied by the front end server, thus eliminating cross-origin issues and allowing cookies to be used for authentication.
In production, nginx is used to serve the front end and proxy requests to the web API.


The <span>ASP.</span>NET Core app was created with the following dotnet CLI command:

 ```dotnet new webapi --exclude-launch-settings --framework net6.0 --use-program-main```

The Vue.js/Nuxt UI was created with the following npm command:

```npm init nuxt-app@latest ./src/ui```

## Build
In development, the solution can be run using Docker Linux containers by executing the following command at the repo root:

```docker-compose up```

...and then opening your browser to https://localhost:10000.

If your browser warns you the site is unsafe, you can either "proceed as unsafe" or add the development certificate to
your certificate store to avoid the warning again. This development certificate must not be used in production!
It was created using the `dotnet dev-certs https` CLI command.

### Server/API
The following environment variables must be configured, at run time, for proper operation:

* ASPNETCORE_ENVIRONMENT
  * "Development" or "Production"
* APP_URL
  * The HTTPS URL to the client/front-end app, i.e. https://localhost:10000
* SQLCONNSTR_DEFAULT (Development only)
  * The connection string to the database
* SQLAZURECONNSTR_DEFAULT (Production Only)
  * The connection string to the Azure SQL database
* AZURE_KEY_VAULT_ID (Production Only)
  * The Azure Key vault identifier. Key Vault encrpyts data protection keys at rest.

### Client/UI
The following environment variables must be configured, at build time, for proper operation:

* NODE_ENV
  * "development" or "production"
* API_URL
  * The URL to the server/web API, i.e. http://localhost:10001. This is only needed by the reverse
  proxy - front-end does not know about this URL.

For development, all environment variables have already been set in the docker compose file and can
be tweaked as needed. Some other environment variables, not listed above, are required for development and
have also been set in the docker-compose file.


## Database Migrations
You will need to add migrations whenever making schema changes to the database. To add a migration, run the following command
within the "api" project:

```dotnet ef migrations add NameOfNewMigration --project ../database -- ConnectionStrings:Default="Data Source=Dummy String"```

Due to the way the EF Core CLI tool acquires the DbContext, you will need to pass a connection string to any command or the
command will fail. Fortunately, the add migration command doesn't really need a database, so you can pass a bogus string.
Other commands might fail without a valid connection string. See the [EF Core Tools](https://learn.microsoft.com/en-us/ef/core/cli/dotnet).

The app has been configured to update the database automatically (apply all pending migrations) at startup.

## Notes

To run any dotnet CLI command, you will need version 6.0.x of the .NET SDK installed on your machine. You can get it
[here](https://dotnet.microsoft.com/en-us/download/dotnet/6.0).

* TODO: Finalize Production Deployment
  * Configure proxy with nginx in.
  * Setup ARM template
  * Setup DevOps build pipeline
  * Setup custom domain?
* TODO: Setup <span>ASP.</span>NET core debugging w/docker
* TODO: Implement captcha for create account


The following documentation was helpful to setup this project:

[Docker for <span>ASP.</span>NET Core](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/docker/building-net-docker-images?view=aspnetcore-6.0)

[Host <span>ASP.</span>NET Core with HTTPS in Docker](https://github.com/dotnet/dotnet-docker/blob/main/samples/host-aspnetcore-https.md)

[Configuring SQL Server for Docker](https://learn.microsoft.com/en-us/sql/linux/sql-server-linux-docker-container-configure)

[Sync Container Startup in Docker Compose](https://github.com/vishnubob/wait-for-it)