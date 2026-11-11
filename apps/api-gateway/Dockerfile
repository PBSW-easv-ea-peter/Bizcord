FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY src/ApiGateway/api-gateway.csproj src/
RUN dotnet restore src/api-gateway.csproj
COPY ./src/ApiGateway/ ./src/
RUN dotnet publish src/api-gateway.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Bizcord.ApiGateway.dll"]
