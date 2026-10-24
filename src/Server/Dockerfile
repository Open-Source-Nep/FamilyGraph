# Production image: Server serving the built Client from wwwroot (D-011).
# The build context is src/:  docker build -f src/Server/Dockerfile src

# 1. Client: the production Angular build.
FROM node:24-alpine AS client
WORKDIR /src/client
COPY client/package.json client/package-lock.json ./
RUN npm ci
COPY client/ ./
RUN npm run build

# 2. Server: restore, then publish with the Client's output in wwwroot.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY FamilyGraph.Business/FamilyGraph.Business.csproj FamilyGraph.Business/
COPY FamilyGraph.Application/FamilyGraph.Application.csproj FamilyGraph.Application/
COPY FamilyGraph.Infrastructure/FamilyGraph.Infrastructure.csproj FamilyGraph.Infrastructure/
COPY Server/Server.csproj Server/
RUN dotnet restore Server/Server.csproj
COPY FamilyGraph.Business/ FamilyGraph.Business/
COPY FamilyGraph.Application/ FamilyGraph.Application/
COPY FamilyGraph.Infrastructure/ FamilyGraph.Infrastructure/
COPY Server/ Server/
COPY --from=client /src/client/dist/client/browser/ Server/wwwroot/
RUN dotnet publish Server/Server.csproj --configuration Release --no-restore --output /app
# The EF Core migration bundle (D-011). It reads the same Database__* settings as the app; the host
# setting here only lets the design-time host build, nothing connects.
RUN dotnet tool install --global dotnet-ef --version 10.0.4
RUN Database__Host=build /root/.dotnet/tools/dotnet-ef migrations bundle \
    --project FamilyGraph.Infrastructure --startup-project Server \
    --configuration Release --no-build --output /app/efbundle

# 3. Runtime: ASP.NET Core only, as the image's non-root user. Run ./efbundle as a one-off step to
#    migrate, or set FAMILYGRAPH_MIGRATE=true to run it before every start.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
COPY --chmod=755 Server/docker-entrypoint.sh ./
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["./docker-entrypoint.sh"]
