# Verificação do passo 7 do docs\IIS.md. Correr no servidor, com o agente a funcionar. Sai com 1 se algum teste falhar.
param(
    [Parameter(Mandatory)][string]$Nome,        # ex.: flow.inforap.local
    [Parameter(Mandatory)][string]$Login,       # um login em perfis.json, ex.: INFORAP\diana.silva
    [string]$Pasta = 'C:\Flow',
    [int]$Porta = 5080,
    [switch]$LinkAssinado                       # demonstração (Instalar-IIS.ps1 -LinkAssinado): testa o link do Visual em vez do Windows Auth
)
$ErrorActionPreference = 'Stop'
$falhas = 0

function Pedir($url, $cabecalhos = @{}, [switch]$Windows, [switch]$SemRedirecionar) {
    $p = @{ Uri = $url; Headers = $cabecalhos; UseBasicParsing = $true; TimeoutSec = 30 }
    if ($Windows) { $p.UseDefaultCredentials = $true }
    if ($SemRedirecionar) { $p.MaximumRedirection = 0 }
    try { $r = Invoke-WebRequest @p; @{ Estado = [int]$r.StatusCode; Corpo = $r.Content } }
    catch { @{ Estado = $(if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 }); Corpo = '' } }
}
function Teste($descricao, [bool]$passou, $detalhe = '') {
    if ($passou) { Write-Host "OK     $descricao" -ForegroundColor Green }
    else { Write-Host "FALHOU $descricao  $detalhe" -ForegroundColor Red; $script:falhas++ }
}

$segredo = [regex]::Match((Get-Content (Join-Path $Pasta 'Iniciar-Flow.cmd') -Raw), 'VENDAS_PROXY_SEGREDO=([0-9a-f]{32,})').Groups[1].Value
Teste 'segredo lido do Iniciar-Flow.cmd' ($segredo.Length -ge 32) 'preencha-o (Instalar-IIS.ps1)'

$ouve = @(Get-NetTCPConnection -LocalPort $Porta -State Listen -ErrorAction SilentlyContinue)
Teste "o agente ouve na porta $Porta" ($ouve.Count -gt 0) 'a tarefa "Flow (agente)" está a correr? Veja flow.log'
Teste "a porta $Porta só em loopback" (@($ouve | Where-Object { $_.LocalAddress -notin '127.0.0.1', '::1' }).Count -eq 0) 'VENDAS_ENDERECO tem de ser http://localhost'

$r = Pedir "http://localhost:$Porta/"
Teste '1  agente sem cabeçalhos de identidade -> 403' ($r.Estado -eq 403) "recebeu $($r.Estado)"
$r = Pedir "http://localhost:$Porta/" @{ 'X-Remote-User' = $Login; 'X-Proxy-Secret' = 'segredo-errado' }
Teste '1b agente com segredo errado -> 403' ($r.Estado -eq 403) "recebeu $($r.Estado)"
$r = Pedir "http://localhost:$Porta/" @{ 'X-Remote-User' = $Login; 'X-Proxy-Secret' = $segredo }
if (-not $LinkAssinado) {   # em modo link o agente ignora estes cabeçalhos (testado em 4b)
    Teste "2  agente com o segredo certo e $Login -> 200" ($r.Estado -eq 200) "recebeu $($r.Estado) (403: $Login falta em perfis.json; reinicie a tarefa depois de o alterar)"
}

if ($LinkAssinado) {
    $seg = [regex]::Match((Get-Content (Join-Path $Pasta 'Iniciar-Flow.cmd') -Raw), 'VENDAS_LINK_SEGREDO=([0-9a-f]{32,})').Groups[1].Value
    Teste 'segredo do link lido do Iniciar-Flow.cmd' ($seg.Length -ge 32) 'corra o Instalar-IIS.ps1 com -LinkAssinado'
    function NovoLink($u, $validade = 30) {
        $e = [DateTimeOffset]::UtcNow.AddSeconds($validade).ToUnixTimeSeconds()
        $h = [Security.Cryptography.HMACSHA256]::new([Text.Encoding]::UTF8.GetBytes($seg))
        $s = -join ($h.ComputeHash([Text.Encoding]::UTF8.GetBytes("$u|$e")) | ForEach-Object { $_.ToString('x2') })
        "https://$Nome/?u=$([uri]::EscapeDataString($u))&e=$e&s=$s"
    }
    $r = Pedir "https://$Nome/"
    Teste '3  sem link nem cookie -> 403' ($r.Estado -eq 403) "recebeu $($r.Estado)"
    $link = NovoLink $Login
    $r = Pedir $link -SemRedirecionar
    Teste "3b link válido de $Login -> 302 (cookie e redireciona)" ($r.Estado -eq 302) "recebeu $($r.Estado) (403: $Login falta em perfis.json, segredo diferente ou relógio desfasado)"
    $r = Pedir $link -SemRedirecionar
    Teste '3c o mesmo link, 2.ª vez -> 403 (uso único)' ($r.Estado -eq 403) "recebeu $($r.Estado)"
    $r = Pedir (NovoLink $Login -60) -SemRedirecionar
    Teste '3d link expirado -> 403' ($r.Estado -eq 403) "recebeu $($r.Estado)"
    $r = Pedir ((NovoLink $Login) -replace 'u=[^&]*', 'u=outro') -SemRedirecionar
    Teste '4  login trocado no link (a assinatura não bate) -> 403' ($r.Estado -eq 403) "recebeu $($r.Estado)"
    $r = Pedir "https://$Nome/" @{ 'X-Remote-User' = $Login; 'X-Proxy-Secret' = $segredo }
    Teste '4b cabeçalho de identidade ignorado neste modo -> 403' ($r.Estado -eq 403) "recebeu $($r.Estado)"
} else {
    $r = Pedir "https://$Nome/" -Windows
    Teste "3  https://$Nome/ com o login Windows -> 200" ($r.Estado -eq 200) "recebeu $($r.Estado) (401: SPN ou loopback check, teste de um posto; 502: agente parado)"
    $forjado = 'INFORAP\outro.utilizador'
    $r = Pedir "https://$Nome/" @{ 'X-Remote-User' = $forjado } -Windows
    Teste '4  cabeçalho X-Remote-User forjado é ignorado' ($r.Estado -eq 200 -and -not $r.Corpo.Contains($forjado)) "recebeu $($r.Estado)"
}
$r = Pedir "http://${Nome}/"
Teste '5  http (sem TLS) não serve a página' ($r.Estado -ne 200) 'o site está acessível por http'

Write-Host "`nManuais: (a) numa pergunta do chat a resposta aparece aos poucos; (b) do posto, 'http://servidor:$Porta/' não liga; (c) auditoria.log mostra o login real."
if ($falhas) { Write-Host "$falhas teste(s) falharam." -ForegroundColor Red; exit 1 }
Write-Host 'Tudo certo.' -ForegroundColor Green
