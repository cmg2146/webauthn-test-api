FROM mcr.microsoft.com/dotnet/sdk:6.0-bullseye-slim AS dotnet-build

ENV ASPNETCORE_ENVIRONMENT=Production
ENV Logging__LogLevel__Default=Information

# Restore first for caching when dependencies dont change
WORKDIR /src
COPY ./src/api/WebAuthnTest.Api.csproj ./api/WebAuthnTest.Api.csproj
COPY ./src/database/WebAuthnTest.Database.csproj ./database/WebAuthnTest.Database.csproj
WORKDIR /src/api
RUN ["dotnet", "restore", "--force"]

# copy everything else and build app
WORKDIR /src
COPY ./src/api ./api
COPY ./src/database ./database
WORKDIR /src/api
RUN ["dotnet", "publish", "--no-restore", "-c", "Release", "-o", "/app"]

# final stage/image
FROM mcr.microsoft.com/dotnet/aspnet:6.0-bullseye-slim
WORKDIR /app
COPY --from=dotnet-build /app ./

CMD ["dotnet", "WebAuthnTest.Api.dll"]
