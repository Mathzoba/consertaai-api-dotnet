#!/usr/bin/env bash
# =====================================================================================
# Conserta Aí — API .NET — provisionamento no Azure via Azure CLI
#
# Cria: Resource Group, Azure SQL (Server + Database), Azure Container Registry,
#       a primeira imagem da API (az acr build) e a Azure Container Instance que roda a API.
#       Opcional: uma ACI com RabbitMQ (CRIAR_RABBITMQ=true).
#
# Uso (Azure Cloud Shell ou terminal com "az login" feito), a partir da RAIZ do repositório:
#   export RM=556649
#   export SQL_PASSWORD='<senha forte>'          # nunca commitar
#   export JWT_SECRET='<mesmo segredo da API Java, 32+ caracteres>'
#   export MONGO_URI='mongodb+srv://...'          # opcional (MongoDB Atlas)
#   bash scripts/infra.sh
#
# Para apagar tudo no fim:  az group delete --name rg-consertaai --yes --no-wait
# =====================================================================================
set -euo pipefail

RM="${RM:?Defina a variável RM (ex.: export RM=556649)}"
LOCATION="${LOCATION:-brazilsouth}"
RG="${RG:-rg-consertaai}"

SQL_SERVER="${SQL_SERVER:-sql-consertaai-$RM}"
SQL_DB="${SQL_DB:-consertaai}"
SQL_ADMIN="${SQL_ADMIN:-consertaaiadmin}"
SQL_PASSWORD="${SQL_PASSWORD:?Defina SQL_PASSWORD (senha do administrador do Azure SQL)}"

ACR="${ACR:-acrconsertaai$RM}"
IMAGEM="consertaai-api-dotnet"
ACI="${ACI:-aci-consertaai-dotnet-$RM}"
DNS_LABEL="${DNS_LABEL:-consertaai-dotnet-$RM}"

JWT_SECRET="${JWT_SECRET:?Defina JWT_SECRET (mesmo valor usado pela API Java)}"
JWT_ISSUER="${JWT_ISSUER:-consertaai}"
JWT_AUDIENCE="${JWT_AUDIENCE:-consertaai-app}"
MONGO_URI="${MONGO_URI:-}"
API_JAVA_URL="${API_JAVA_URL:-}"
CRIAR_RABBITMQ="${CRIAR_RABBITMQ:-false}"
HABILITAR_TOKEN_TESTE="${HABILITAR_TOKEN_TESTE:-true}"

echo ">> 1/6 Resource Group $RG ($LOCATION)"
az group create --name "$RG" --location "$LOCATION" --output none

echo ">> 2/6 Azure SQL Server $SQL_SERVER + Database $SQL_DB"
az sql server create \
  --name "$SQL_SERVER" --resource-group "$RG" --location "$LOCATION" \
  --admin-user "$SQL_ADMIN" --admin-password "$SQL_PASSWORD" --output none

# Libera o acesso a partir de serviços do Azure (ACI / Pipelines)
az sql server firewall-rule create \
  --resource-group "$RG" --server "$SQL_SERVER" \
  --name AllowAzureServices --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0 --output none

az sql db create \
  --resource-group "$RG" --server "$SQL_SERVER" --name "$SQL_DB" \
  --service-objective Basic --backup-storage-redundancy Local --output none

CONNECTION_STRING="Server=tcp:${SQL_SERVER}.database.windows.net,1433;Initial Catalog=${SQL_DB};User ID=${SQL_ADMIN};Password=${SQL_PASSWORD};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

echo ">> 3/6 Azure Container Registry $ACR"
az acr create --resource-group "$RG" --name "$ACR" --sku Basic --admin-enabled true --location "$LOCATION" --output none
ACR_SERVER=$(az acr show --name "$ACR" --query loginServer --output tsv)
ACR_USER=$(az acr credential show --name "$ACR" --query username --output tsv)
ACR_PASS=$(az acr credential show --name "$ACR" --query "passwords[0].value" --output tsv)

echo ">> 4/6 Primeira imagem da API (az acr build) — depois o CI publica as próximas"
az acr build --registry "$ACR" --image "$IMAGEM:inicial" --image "$IMAGEM:latest" --file dockerfiles/Dockerfile . --output none

RABBIT_ENV=()
if [[ "$CRIAR_RABBITMQ" == "true" ]]; then
  echo ">> 5/6 RabbitMQ em Container Instance"
  RABBIT_PASSWORD="${RABBIT_PASSWORD:?Defina RABBIT_PASSWORD para criar o RabbitMQ}"
  az container create \
    --resource-group "$RG" --name "aci-consertaai-rabbitmq-$RM" \
    --image rabbitmq:3.13-management --os-type Linux --cpu 1 --memory 1 \
    --ports 5672 15672 --dns-name-label "consertaai-rabbit-$RM" \
    --environment-variables RABBITMQ_DEFAULT_USER=consertaai \
    --secure-environment-variables RABBITMQ_DEFAULT_PASS="$RABBIT_PASSWORD" --output none
  RABBIT_HOST=$(az container show --resource-group "$RG" --name "aci-consertaai-rabbitmq-$RM" --query ipAddress.fqdn --output tsv)
  RABBIT_ENV=(Eventos__Modo=RabbitMq RabbitMq__HostName="$RABBIT_HOST" RabbitMq__UserName=consertaai)
else
  echo ">> 5/6 RabbitMQ não criado (CRIAR_RABBITMQ=false): eventos ficam no log da API"
fi

echo ">> 6/6 Azure Container Instance $ACI (API .NET)"
# Variáveis comuns e variáveis SECRETAS (não aparecem no portal nem no "az container show")
ENV=(ASPNETCORE_ENVIRONMENT=Production BancoDados__Provedor=SqlServer
     JWT_ISSUER="$JWT_ISSUER" JWT_AUDIENCE="$JWT_AUDIENCE"
     Auth__EmissorTokenTesteHabilitado="$HABILITAR_TOKEN_TESTE")
if [[ -n "$API_JAVA_URL" ]]; then ENV+=(ApiJava__BaseUrl="$API_JAVA_URL"); fi
if [[ ${#RABBIT_ENV[@]} -gt 0 ]]; then ENV+=("${RABBIT_ENV[@]}"); fi

SECURE_ENV=(ConnectionStrings__ConsertaAi="$CONNECTION_STRING" JWT_SECRET="$JWT_SECRET")
if [[ -n "$MONGO_URI" ]]; then SECURE_ENV+=(MongoDbSettings__ConnectionString="$MONGO_URI"); fi
if [[ "$CRIAR_RABBITMQ" == "true" ]]; then SECURE_ENV+=(RabbitMq__Password="$RABBIT_PASSWORD"); fi

az container create \
  --resource-group "$RG" --name "$ACI" \
  --image "$ACR_SERVER/$IMAGEM:latest" \
  --registry-login-server "$ACR_SERVER" --registry-username "$ACR_USER" --registry-password "$ACR_PASS" \
  --os-type Linux --cpu 1 --memory 1.5 --ports 8080 --dns-name-label "$DNS_LABEL" \
  --environment-variables "${ENV[@]}" \
  --secure-environment-variables "${SECURE_ENV[@]}" \
  --output none

FQDN=$(az container show --resource-group "$RG" --name "$ACI" --query ipAddress.fqdn --output tsv)
echo ""
echo "Pronto!"
echo "  Swagger: http://$FQDN:8080/swagger"
echo "  Health : http://$FQDN:8080/health"
echo "  ACR    : $ACR_SERVER"
