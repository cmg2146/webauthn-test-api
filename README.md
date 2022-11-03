# Introduction 
TODO: Give a short introduction of your project. Let this section explain the objectives or the motivation behind this project. 

# Getting Started
TODO: Guide users through getting your code up and running on their own system. In this section you can talk about:
1.	Installation process
2.	Software dependencies
3.	Latest releases
4.	API references

# Build and Test
TODO: Describe and show how to build your code and run the tests. 

# Contribute

Environment Variables

ASPNETCORE_ENVIRONMENT
SQLCONNSTR_DEFAULT
SQLAZURECONNSTR_DEFAULT (Production)
AZURE_KEY_VAULT_ID (Production)
APP_URL

The "api" project was created with the following dotnet CLI command:

 ```dotnet new webapi --exclude-launch-settings --framework net6.0 --use-program-main```

### EF Core Migrations
You will need to add migrations whenever making schema changes to the database. To add a migration, run the following command
within the "api" project:

```dotnet ef migrations add NameOfNewMigration --project ../database -- ConnectionStrings:Default="Data Source=Dummy String"```

Due to the way the EF Core CLI tool acquires the DbContext, you will need to pass a connection string to any command or the
command will fail. Fortunately, the add migration command doesn't really need a database, so you can pass a bogus string.
Other commands might fail without a valid connection string. See the [EF Core Tools](https://learn.microsoft.com/en-us/ef/core/cli/dotnet).

TODO: Setup docker
TODO: Setup https
TODO: Setup debugging
TODO: Setup ARM template, possibly with build pipeline