# =========================
# BUILD STAGE
# =========================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY *.sln .
COPY WordParser/*.csproj ./WordParser/
COPY WordParserWeb/*.csproj ./WordParserWeb/
COPY WordParserCore/*.csproj ./WordParserCore/
COPY ModelDto/*.csproj ./ModelDto/
COPY WordParserCore.Tests/*.csproj ./WordParserCore.Tests/

RUN dotnet restore WordParserWeb/WordParserWeb.csproj

COPY . .

RUN dotnet publish WordParserWeb/WordParserWeb.csproj \
    -c Release \
    -o /app/publish

# =========================
# RUNTIME STAGE
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:10.0

WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8000

EXPOSE 8000

ENTRYPOINT ["dotnet", "WordParserWeb.dll"]
