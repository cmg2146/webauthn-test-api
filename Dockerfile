FROM mcr.microsoft.com/dotnet/sdk:6.0 AS dotnet-build

ENV ASPNETCORE_ENVIRONMENT=Production

WORKDIR /src

###TODO:Cannot get publish to work when doing a restore first
# # do restore as separate step for caching
# COPY ./src/api/WebAuthnTest.Api.csproj ./api/
# COPY ./src/database/WebAuthnTest.Database.csproj ./database/
# WORKDIR /src/api
# RUN ["dotnet", "restore", "--force"]

# copy everything else and build app
WORKDIR /src
COPY ./src/api ./api
COPY ./src/database ./database
WORKDIR /src/api
RUN ["dotnet", "publish", "-c", "release", "-o", "/app"]
#, "--no-restore"]

# final stage/image
FROM mcr.microsoft.com/dotnet/aspnet:6.0 as dotnet-run
WORKDIR /app
COPY --from=dotnet-build /app ./

CMD ["dotnet", "WebAuthnTest.Api.dll"]