# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY KorridorX.csproj ./
RUN dotnet restore KorridorX.csproj

COPY . .
RUN dotnet publish KorridorX.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080

COPY --from=build --chown=app:app /app/publish .

USER root
RUN mkdir -p /var/lib/korridorx/keys && chown -R app:app /var/lib/korridorx
USER app
ENTRYPOINT ["dotnet", "KorridorX.dll"]
