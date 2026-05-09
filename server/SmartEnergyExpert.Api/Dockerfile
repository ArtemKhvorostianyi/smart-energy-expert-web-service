# Build context = repository root (Sliplane / GitHub: Dockerfile path server/SmartEnergyExpert.Api/Dockerfile).
# Local: docker build -f server/SmartEnergyExpert.Api/Dockerfile -t hydro-api .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY data/ data/
COPY server/SmartEnergyExpert.Api/SmartEnergyExpert.Api.csproj server/SmartEnergyExpert.Api/
RUN dotnet restore server/SmartEnergyExpert.Api/SmartEnergyExpert.Api.csproj
COPY server/SmartEnergyExpert.Api/ server/SmartEnergyExpert.Api/
WORKDIR /src/server/SmartEnergyExpert.Api
RUN dotnet publish SmartEnergyExpert.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "SmartEnergyExpert.Api.dll"]
