# Build context is the repo root, like the services (uses the root .dockerignore):
#   docker build -f dev/token-issuer/Dockerfile .

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Only the csproj first, so the restore layer stays cached until the dependencies change.
COPY ["dev/token-issuer/TokenIssuer.csproj", "dev/token-issuer/"]
RUN dotnet restore "dev/token-issuer/TokenIssuer.csproj"

COPY ["dev/token-issuer/", "dev/token-issuer/"]
WORKDIR "/src/dev/token-issuer"
RUN dotnet publish "TokenIssuer.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TokenIssuer.dll"]
