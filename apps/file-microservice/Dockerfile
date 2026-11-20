# Build context is the repo root (FileService references libs/Bizcord.Logging):
#   docker build -f apps/file-microservice/Dockerfile .

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Only the csproj files first, so the restore layer stays cached until dependencies change.
COPY ["apps/file-microservice/src/FileService/FileService.csproj", "apps/file-microservice/src/FileService/"]
COPY ["libs/Bizcord.Logging/Bizcord.Logging.csproj", "libs/Bizcord.Logging/"]
RUN dotnet restore "apps/file-microservice/src/FileService/FileService.csproj"

COPY ["apps/file-microservice/src/FileService/", "apps/file-microservice/src/FileService/"]
COPY ["libs/Bizcord.Logging/", "libs/Bizcord.Logging/"]
WORKDIR "/src/apps/file-microservice/src/FileService"
RUN dotnet build "FileService.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "FileService.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FileService.dll"]
