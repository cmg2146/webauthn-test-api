FROM mcr.microsoft.com/dotnet/sdk:6.0-bullseye-slim AS build

ARG BUILD_CONFIGURATION=Release
ENV BUILD_CONFIGURATION=$BUILD_CONFIGURATION

WORKDIR /app

# do dependency installation as separate step for caching
COPY ./src/api/WebAuthnTest.Api.csproj ./src/api/WebAuthnTest.Api.csproj
COPY ./src/database/WebAuthnTest.Database.csproj ./src/database/WebAuthnTest.Database.csproj
RUN ["dotnet", "restore", "./src/api", "--force"]

# copy everything else and build app
COPY ./src ./src
RUN ["dotnet", "publish", "./src/api", "--no-restore", "-c", "$BUILD_CONFIGURATION", "-o", "/app/dist"]


# final stage
FROM mcr.microsoft.com/dotnet/aspnet:6.0-bullseye-slim

ENV ASPNETCORE_ENVIRONMENT=Production
ENV Logging__LogLevel__Default=Information

WORKDIR /app
COPY --from=build /app/dist ./

CMD ["dotnet", "WebAuthnTest.Api.dll"]
