#!/usr/bin/env bash

# apply database migrations in the background
dotnet WebAuthnTest.Api.dll --migrate-database &

# start the api
dotnet WebAuthnTest.Api.dll
