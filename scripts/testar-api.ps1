# =====================================================================================
# Conserta Aí — API .NET — teste ponta a ponta de todos os requisitos (PowerShell)
#
# 1) Em um terminal, suba a API:   dotnet run --project src/ConsertaAi.Api
# 2) Em outro terminal, rode:       powershell -ExecutionPolicy Bypass -File scripts/testar-api.ps1
#    (Docker compose: use -Base http://localhost:8080)
# =====================================================================================
param([string]$Base = "http://localhost:5080")

$ErrorActionPreference = "Stop"
$script:ok = 0
$script:falhas = 0
$rodada = Get-Random -Minimum 10000 -Maximum 99999   # ids únicos: o script pode ser executado várias vezes

function Chamar([string]$Metodo, [string]$Url, [string]$Token, $Corpo, [string]$Chave) {
    $headers = @{}
    if ($Token) { $headers["Authorization"] = "Bearer $Token" }
    if ($Chave) { $headers["Idempotency-Key"] = $Chave }
    $params = @{ Method = $Metodo; Uri = "$Base$Url"; Headers = $headers; UseBasicParsing = $true; ContentType = "application/json; charset=utf-8" }
    if ($null -ne $Corpo) { $params.Body = [Text.Encoding]::UTF8.GetBytes(($Corpo | ConvertTo-Json -Depth 5 -Compress)) }
    try {
        $r = Invoke-WebRequest @params
        return [pscustomobject]@{ Status = [int]$r.StatusCode; Corpo = $r.Content; Headers = $r.Headers }
    } catch {
        $resp = $_.Exception.Response
        if ($null -eq $resp) { throw }
        $leitor = New-Object IO.StreamReader($resp.GetResponseStream())
        return [pscustomobject]@{ Status = [int]$resp.StatusCode; Corpo = $leitor.ReadToEnd(); Headers = $resp.Headers }
    }
}

function Verificar([string]$Nome, $Obtido, $Esperado) {
    if ($Obtido -eq $Esperado) { Write-Host "[OK]      $Nome -> $Obtido" -ForegroundColor Green; $script:ok++ }
    else { Write-Host "[FALHOU]  $Nome -> obtido '$Obtido', esperado '$Esperado'" -ForegroundColor Red; $script:falhas++ }
}

function Token([string]$Id, [string]$Perfil) {
    $r = Chamar POST "/auth/token-teste" $null @{ usuarioId = $Id; nome = "Usuario $Id"; perfil = $Perfil }
    return ($r.Corpo | ConvertFrom-Json).accessToken
}

function Rels($Json) { return @($Json.links | ForEach-Object { $_.rel }) }

$ocorrencia = $rodada
$risco = "8f6f6c1e-3b1a-4c55-9d0a-1a2b3c4d5e01"
$piorou = "8f6f6c1e-3b1a-4c55-9d0a-1a2b3c4d5e04"

Write-Host "`n=== 1. Health Checks (10 pts)" -ForegroundColor Cyan
$h = Chamar GET "/health"
Verificar "GET /health" $h.Status 200
Write-Host ("          status geral: " + ($h.Corpo | ConvertFrom-Json).status)

Write-Host "`n=== 2. Tokens de teste (cidadao, outro cidadao, gestor)" -ForegroundColor Cyan
$cidadao = Token "$rodada-1" "CIDADAO"
$outro   = Token "$rodada-2" "CIDADAO"
$gestor  = Token "$rodada-9" "GESTOR"
Verificar "3 tokens emitidos" (@($cidadao, $outro, $gestor) | Where-Object { $_ }).Count 3

Write-Host "`n=== 3. JWT: autenticacao e autorizacao (20 pts)" -ForegroundColor Cyan
Verificar "POST /apoios sem token (401)" (Chamar POST "/apoios" $null @{ ocorrenciaId = $ocorrencia } "k-$rodada-0").Status 401
Verificar "POST /apoios com token invalido (401)" (Chamar POST "/apoios" "token.invalido.123" @{ ocorrenciaId = $ocorrencia } "k-$rodada-0").Status 401
Verificar "POST /apoios como GESTOR (403)" (Chamar POST "/apoios" $gestor @{ ocorrenciaId = $ocorrencia } "k-$rodada-0").Status 403

Write-Host "`n=== 4. Validacoes e codigos HTTP (20 pts)" -ForegroundColor Cyan
# Usuario separado: cada usuario so pode fazer 5 POSTs por minuto (rate limit)
$validacao = Token "$rodada-3" "CIDADAO"
Verificar "POST /apoios sem Idempotency-Key (400)" (Chamar POST "/apoios" $validacao @{ ocorrenciaId = $ocorrencia } $null).Status 400
Verificar "POST /apoios com ocorrenciaId = 0 (400)" (Chamar POST "/apoios" $validacao @{ ocorrenciaId = 0 } "k-$rodada-v").Status 400

Write-Host "`n=== 5. Idempotencia (15 pts)" -ForegroundColor Cyan
$chave = "k-$rodada-1"
$p1 = Chamar POST "/apoios" $cidadao @{ ocorrenciaId = $ocorrencia } $chave
$p2 = Chamar POST "/apoios" $cidadao @{ ocorrenciaId = $ocorrencia } $chave
Verificar "1o POST /apoios (201)" $p1.Status 201
Verificar "2o POST com a MESMA chave (201 repetido)" $p2.Status 201
Verificar "Header Idempotency-Replayed" $p2.Headers["Idempotency-Replayed"] "true"
Verificar "Mesma resposta nas duas chamadas" ($p1.Corpo -eq $p2.Corpo) $true
Verificar "Mesma chave com corpo diferente (422)" (Chamar POST "/apoios" $cidadao @{ ocorrenciaId = $ocorrencia + 1 } $chave).Status 422
Verificar "Chave nova, apoio repetido (409)" (Chamar POST "/apoios" $cidadao @{ ocorrenciaId = $ocorrencia } "k-$rodada-2").Status 409

Write-Host "`n=== 6. HATEOAS e resumo de apoios" -ForegroundColor Cyan
$resumo = (Chamar GET "/ocorrencias/$ocorrencia/apoios" $cidadao).Corpo | ConvertFrom-Json
Verificar "Total de apoios" $resumo.data.totalApoios 1
Verificar "Link 'retirar-apoio' para quem ja apoiou" ((Rels $resumo) -contains "retirar-apoio") $true
$resumoOutro = (Chamar GET "/ocorrencias/$ocorrencia/apoios" $outro).Corpo | ConvertFrom-Json
Verificar "Link 'apoiar' para quem ainda nao apoiou" ((Rels $resumoOutro) -contains "apoiar") $true

Write-Host "`n=== 7. CRUD de comentarios + etiquetas N:N" -ForegroundColor Cyan
$c = Chamar POST "/comentarios" $outro @{ ocorrenciaId = $ocorrencia; texto = "O buraco aumentou depois da chuva"; etiquetaIds = @($risco, $piorou) } "k-$rodada-c1"
Verificar "POST /comentarios com 2 etiquetas (201)" $c.Status 201
$comentario = ($c.Corpo | ConvertFrom-Json).data
Verificar "Etiquetas gravadas (N:N)" @($comentario.etiquetas).Count 2
Verificar "PUT por outro usuario (403)" (Chamar PUT "/comentarios/$($comentario.id)" $cidadao @{ texto = "Texto adulterado" }).Status 403
Verificar "PUT pelo autor (200)" (Chamar PUT "/comentarios/$($comentario.id)" $outro @{ texto = "O buraco aumentou muito depois da chuva"; etiquetaIds = @($risco) }).Status 200
$lista = (Chamar GET "/ocorrencias/$ocorrencia/comentarios?pagina=1&tamanhoPagina=5" $outro).Corpo | ConvertFrom-Json
Verificar "GET lista paginada (total)" $lista.totalRegistros 1
Verificar "Autor recebe link 'update'" ((Rels $lista.itens[0]) -contains "update") $true
Verificar "Texto ofensivo bloqueado pela moderacao (400)" (Chamar POST "/comentarios" $outro @{ ocorrenciaId = $ocorrencia; texto = "o responsavel e um idiota" } "k-$rodada-c2").Status 400
Verificar "DELETE por outro cidadao (403)" (Chamar DELETE "/comentarios/$($comentario.id)" $cidadao).Status 403
Verificar "DELETE pelo GESTOR (204)" (Chamar DELETE "/comentarios/$($comentario.id)" $gestor).Status 204
Verificar "GET comentario removido (404)" (Chamar GET "/comentarios/$($comentario.id)" $outro).Status 404

Write-Host "`n=== 8. CRUD de etiquetas (somente GESTOR)" -ForegroundColor Cyan
Verificar "POST /etiquetas como CIDADAO (403)" (Chamar POST "/etiquetas" $cidadao @{ nome = "Etiqueta $rodada" }).Status 403
$e = Chamar POST "/etiquetas" $gestor @{ nome = "Etiqueta $rodada"; descricao = "Criada pelo teste" }
Verificar "POST /etiquetas como GESTOR (201)" $e.Status 201
$etiquetaId = ($e.Corpo | ConvertFrom-Json).data.id
Verificar "POST etiqueta com nome repetido (409)" (Chamar POST "/etiquetas" $gestor @{ nome = "ETIQUETA $rodada" }).Status 409
Verificar "PUT /etiquetas/{id} (200)" (Chamar PUT "/etiquetas/$etiquetaId" $gestor @{ nome = "Etiqueta $rodada editada" }).Status 200
Verificar "DELETE /etiquetas/{id} (204)" (Chamar DELETE "/etiquetas/$etiquetaId" $gestor).Status 204

Write-Host "`n=== 9. Linha do tempo (MongoDB)" -ForegroundColor Cyan
$at = (Chamar GET "/ocorrencias/$ocorrencia/atividades" $cidadao).Corpo | ConvertFrom-Json
Verificar "Atividades registradas (apoio + comentario criado/editado/removido)" ($at.totalRegistros -ge 4) $true
Write-Host ("          tipos: " + (@($at.itens | ForEach-Object { $_.data.tipo }) -join ", "))

Write-Host "`n=== 10. Retirar apoio" -ForegroundColor Cyan
Verificar "DELETE /apoios/{id} (204)" (Chamar DELETE "/apoios/$ocorrencia" $cidadao).Status 204
Verificar "DELETE de novo (404)" (Chamar DELETE "/apoios/$ocorrencia" $cidadao).Status 404

Write-Host "`n=== 11. Rate limiting: 5 POSTs por minuto por usuario (15 pts)" -ForegroundColor Cyan
$limitado = Token "$rodada-rl" "CIDADAO"
$codigos = @()
for ($i = 1; $i -le 6; $i++) { $ultimo = Chamar POST "/apoios" $limitado @{ ocorrenciaId = $ocorrencia + 100 + $i } "k-$rodada-rl-$i"; $codigos += $ultimo.Status }
Write-Host ("          codigos: " + ($codigos -join " "))
Verificar "5 primeiros aceitos (201)" (@($codigos[0..4] | Where-Object { $_ -eq 201 }).Count) 5
Verificar "6o bloqueado (429)" $codigos[5] 429
Verificar "Header Retry-After presente" ([bool]$ultimo.Headers["Retry-After"]) $true

Write-Host "`n=== 12. Swagger" -ForegroundColor Cyan
Verificar "GET /swagger/v1/swagger.json (200)" (Chamar GET "/swagger/v1/swagger.json").Status 200

Write-Host ""
$cor = if ($script:falhas -eq 0) { "Green" } else { "Red" }
Write-Host "RESULTADO: $($script:ok) verificacoes OK, $($script:falhas) falha(s)" -ForegroundColor $cor
if ($script:falhas -gt 0) { exit 1 }
