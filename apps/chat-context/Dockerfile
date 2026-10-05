# Build context is the repo root (ChatService references libs/Bizcord.Logging):
#   docker build -f apps/chat-context/Dockerfile .

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Only the csproj files first, so the restore layer stays cached until dependencies change.
COPY ["apps/chat-context/src/ChatService/ChatService.csproj", "apps/chat-context/src/ChatService/"]
COPY ["libs/Bizcord.Logging/Bizcord.Logging.csproj", "libs/Bizcord.Logging/"]
RUN dotnet restore "apps/chat-context/src/ChatService/ChatService.csproj"

COPY ["apps/chat-context/src/ChatService/", "apps/chat-context/src/ChatService/"]
COPY ["libs/Bizcord.Logging/", "libs/Bizcord.Logging/"]
WORKDIR "/src/apps/chat-context/src/ChatService"
RUN dotnet build "ChatService.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "ChatService.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ChatService.dll"]
