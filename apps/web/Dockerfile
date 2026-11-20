# Web frontend. Build context is apps/web (no shared code with the services):
#   docker build -t bizcord/web apps/web
#   docker run -p 8084:8080 -e GatewayApi__BaseUrl="http://<gateway>:8080/" bizcord/web

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Project file first, so the restore layer is reused as long as the packages don't change
COPY src/Bizcord/Bizcord.csproj src/Bizcord/
RUN dotnet restore src/Bizcord/Bizcord.csproj

COPY src/ src/
RUN dotnet publish src/Bizcord/Bizcord.csproj -c $BUILD_CONFIGURATION -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Bizcord.dll"]
