FROM node:22.15.0-alpine AS web
WORKDIR /web
COPY src/web/package*.json ./
RUN npm ci
COPY src/web/ ./
RUN npm run build
FROM mcr.microsoft.com/dotnet/sdk:10.0.404 AS build
WORKDIR /source
COPY . .
RUN dotnet restore src/backend/ClubPlatform.Api --locked-mode
RUN dotnet publish src/backend/ClubPlatform.Api -c Release --no-restore -o /out
FROM mcr.microsoft.com/dotnet/aspnet:10.0.10 AS final
WORKDIR /app
COPY --from=build /out ./
COPY --from=web /web/dist ./wwwroot
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
ENTRYPOINT ["dotnet", "ClubPlatform.Api.dll"]
