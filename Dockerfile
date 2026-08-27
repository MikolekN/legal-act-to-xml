# =========================
# BUILD
# =========================
FROM mcr.microsoft.com/dotnet/sdk:10.0.400 AS build

ARG VERSION=0.0.0
ARG REVISION=""

WORKDIR /src

COPY Directory.Build.props ./
COPY src/Saga.Model/Saga.Model.csproj src/Saga.Model/
COPY src/Saga.Core/Saga.Core.csproj src/Saga.Core/
COPY src/Saga.Web/Saga.Web.csproj src/Saga.Web/
RUN dotnet restore src/Saga.Web/Saga.Web.csproj

COPY src/Saga.Model/ src/Saga.Model/
COPY src/Saga.Core/ src/Saga.Core/
COPY src/Saga.Web/ src/Saga.Web/

RUN dotnet publish src/Saga.Web/Saga.Web.csproj \
        --configuration Release \
        --no-restore \
        --output /app/publish \
        -p:UseAppHost=false \
        -p:InformationalVersion="${VERSION}" \
        -p:SourceRevisionId="${REVISION}"


# =========================
# RUNTIME
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:10.0.11 AS runtime

WORKDIR /app
COPY --from=build /app/publish ./

RUN mkdir -p /app/logs && chown -R $APP_UID:$APP_UID /app/logs

ENV ASPNETCORE_URLS="http://+:8080" \
    DOTNET_RUNNING_IN_CONTAINER=true

USER $APP_UID
EXPOSE 8080

ENTRYPOINT ["dotnet", "Saga.Web.dll"]
