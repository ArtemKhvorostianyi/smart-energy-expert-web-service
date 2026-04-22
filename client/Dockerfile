# Base runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80

# Build frontend stage
FROM node:24-bookworm-slim AS frontend
WORKDIR /src
COPY . .
RUN apt-get update && apt-get install -y curl bash && rm -rf /var/lib/apt/lists/*
RUN curl -fsSL https://vite.plus | bash
ENV PATH="/root/.vite-plus/bin:${PATH}"
RUN if [ -d "frontend" ] && [ -f "frontend/package.json" ]; then cd frontend && vp install && vp build; fi
RUN mkdir -p frontend/dist && touch frontend/dist/.keep

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy and restore
COPY ["SmartEnergyExpert.Client.csproj", "./"]
RUN dotnet restore "SmartEnergyExpert.Client.csproj"

# Copy everything and build
COPY . .
COPY --from=frontend /src/frontend/dist ./frontend/dist
RUN dotnet build "SmartEnergyExpert.Client.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "SmartEnergyExpert.Client.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=true

# Final runtime image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Set environment variables
ENV PORT=80
ENV ASPNETCORE_URLS="http://+:80"

# Run the executable
ENTRYPOINT ["dotnet","./SmartEnergyExpert.Client.dll"]