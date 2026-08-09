# syntax=docker/dockerfile:1

# ---------------------------------------------------------------- Frontend
# Build l'application React (Vite) → src/Factur.Api/wwwroot
FROM node:22-alpine AS frontend
WORKDIR /src/frontend
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

# ---------------------------------------------------------------- Backend
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY Mohasabi.slnx ./
COPY src/ ./src/
# Injection du frontend compilé dans wwwroot avant publication
COPY --from=frontend /src/src/Factur.Api/wwwroot ./src/Factur.Api/wwwroot/
RUN dotnet publish src/Factur.Api/Factur.Api.csproj -c Release -o /app/publish --nologo

# ---------------------------------------------------------------- Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_EnableDiagnostics=0
EXPOSE 8080
ENTRYPOINT ["dotnet", "Mohasabi.Api.dll"]
