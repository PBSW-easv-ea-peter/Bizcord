FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/RealTimeCommunicationServer/RealTimeCommunicationServer.csproj src/RealTimeCommunicationServer/
RUN dotnet restore src/RealTimeCommunicationServer/RealTimeCommunicationServer.csproj

COPY src/RealTimeCommunicationServer/ src/RealTimeCommunicationServer/
RUN dotnet publish src/RealTimeCommunicationServer/RealTimeCommunicationServer.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "RealTimeCommunicationServer.dll"]
