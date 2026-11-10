# =====================================================================================
# Conserta Aí — API .NET (Minimal API) — imagem de produção
# Build a partir da RAIZ do repositório:
#   docker build -f dockerfiles/Dockerfile -t consertaai-api-dotnet .
# =====================================================================================

# ---------- Etapa 1: build e publish com o SDK
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia primeiro só os arquivos de projeto para aproveitar o cache do restore
COPY Directory.Build.props ConsertaAi.sln ./
COPY src/ConsertaAi.Dominio/ConsertaAi.Dominio.csproj src/ConsertaAi.Dominio/
COPY src/ConsertaAi.Aplicacao/ConsertaAi.Aplicacao.csproj src/ConsertaAi.Aplicacao/
COPY src/ConsertaAi.Infraestrutura/ConsertaAi.Infraestrutura.csproj src/ConsertaAi.Infraestrutura/
COPY src/ConsertaAi.Api/ConsertaAi.Api.csproj src/ConsertaAi.Api/
RUN dotnet restore src/ConsertaAi.Api/ConsertaAi.Api.csproj

COPY src/ src/
RUN dotnet publish src/ConsertaAi.Api/ConsertaAi.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- Etapa 2: runtime enxuto (sem SDK), executando com usuário sem privilégios
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

COPY --from=build /app/publish .
RUN mkdir -p /app/logs && chown -R app:app /app/logs

USER app
EXPOSE 8080

ENTRYPOINT ["dotnet", "ConsertaAi.Api.dll"]
