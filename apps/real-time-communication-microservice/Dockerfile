# Build context is the repo root (the service references libs/Bizcord.Logging):
#   docker build -f apps/real-time-communication-microservice/Dockerfile .

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Only the csproj files first, so the restore layer stays cached until the dependencies change.
COPY ["apps/real-time-communication-microservice/src/RealTimeCommunicationServer/RealTimeCommunicationServer.csproj", "apps/real-time-communication-microservice/src/RealTimeCommunicationServer/"]
COPY ["libs/Bizcord.Logging/Bizcord.Logging.csproj", "libs/Bizcord.Logging/"]
RUN dotnet restore "apps/real-time-communication-microservice/src/RealTimeCommunicationServer/RealTimeCommunicationServer.csproj"

COPY ["apps/real-time-communication-microservice/src/RealTimeCommunicationServer/", "apps/real-time-communication-microservice/src/RealTimeCommunicationServer/"]
COPY ["libs/Bizcord.Logging/", "libs/Bizcord.Logging/"]
WORKDIR "/src/apps/real-time-communication-microservice/src/RealTimeCommunicationServer"
RUN dotnet build "RealTimeCommunicationServer.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "RealTimeCommunicationServer.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RealTimeCommunicationServer.dll"]
