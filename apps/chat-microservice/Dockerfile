# Build-context er repo-roden (ChatService refererer til libs/Bizcord.Logging):
#   docker build -f apps/chat-context/Dockerfile .

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Kun csproj-filer først, så restore-laget caches, indtil afhængighederne ændres.
COPY ["apps/chat-microservice/src/ChatService/ChatService.csproj", "apps/chat-microservice/src/ChatService/"]
COPY ["libs/Bizcord.Logging/Bizcord.Logging.csproj", "libs/Bizcord.Logging/"]
RUN dotnet restore "apps/chat-microservice/src/ChatService/ChatService.csproj"

COPY ["apps/chat-microservice/src/ChatService/", "apps/chat-microservice/src/ChatService/"]
COPY ["libs/Bizcord.Logging/", "libs/Bizcord.Logging/"]
WORKDIR "/src/apps/chat-microservice/src/ChatService"
RUN dotnet build "ChatService.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "ChatService.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ChatService.dll"]
