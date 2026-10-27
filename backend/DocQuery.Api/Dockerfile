# Step 1: build the app with the full .NET SDK
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY DocQuery.Api.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app

# Step 2: run it on the smaller runtime-only image
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# Render tells us which port to use through $PORT (8080 if not set)
CMD ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet DocQuery.Api.dll
