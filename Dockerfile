FROM node:24-bookworm-slim AS web-build
WORKDIR /src
COPY src/ServiceDesk.Web/package.json src/ServiceDesk.Web/package-lock.json ./
RUN npm ci
COPY src/ServiceDesk.Web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
WORKDIR /src
COPY src/ServiceDesk.Api/ServiceDesk.Api.csproj ./
RUN dotnet restore ServiceDesk.Api.csproj
COPY src/ServiceDesk.Api/ ./
RUN dotnet publish ServiceDesk.Api.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=api-build /app/publish ./
COPY --from=web-build /src/dist/servicedesk-web/browser/ ./wwwroot/
USER $APP_UID
ENTRYPOINT ["dotnet", "ServiceDesk.Api.dll"]
