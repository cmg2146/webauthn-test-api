FROM mcr.microsoft.com/dotnet/sdk:6.0-bullseye-slim AS dotnet-build

ENV ASPNETCORE_ENVIRONMENT=Production
ENV Logging__LogLevel__Default=Information

WORKDIR /src

# TODO:Cannot get publish to work when doing a restore first

# copy everything else and build app
COPY ./src/api ./api
COPY ./src/database ./database
WORKDIR /src/api
RUN ["dotnet", "publish", "-c", "release", "-o", "/app"]

# final stage/image
FROM mcr.microsoft.com/dotnet/aspnet:6.0-bullseye-slim
WORKDIR /app
COPY --from=dotnet-build /app ./

CMD ["dotnet", "WebAuthnTest.Api.dll"]