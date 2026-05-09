# Build (context = this directory):
#   docker build -t hydro-api -f server/SmartEnergyExpert.Api/Dockerfile server/SmartEnergyExpert.Api
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["SmartEnergyExpert.Api.csproj", "./"]
RUN dotnet restore "./SmartEnergyExpert.Api.csproj"
COPY . .
RUN dotnet publish "./SmartEnergyExpert.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "SmartEnergyExpert.Api.dll"]
