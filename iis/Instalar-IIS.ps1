# Cria o site do Flow no IIS (proxy inverso para o agente em loopback). Como Administrador; começar com -WhatIf.
# Cada passo valida o que fez e pára no primeiro que falhe. Pode repetir-se: o que já existe não é recriado.
# Antes: pacote copiado para -Pasta (Vendas.Agente.exe e servidor\), URL Rewrite e ARR instalados, certificado em LocalMachine\My.
#Requires -RunAsAdministrator
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][string]$Nome,           # nome DNS do site, ex.: flow.inforap.local
    [Parameter(Mandatory)][string]$Thumbprint,     # certificado HTTPS em Cert:\LocalMachine\My
    [Parameter(Mandatory)][string]$ContaPool,      # conta de domínio do pool e da tarefa do agente, ex.: INFORAP\svc-flow
    [string]$Pasta = 'C:\Flow',                    # onde está o agente
    [string]$Site = 'Flow',
    [int]$Porta = 5080,
    [ValidatePattern('^[0-9a-f]{32,}$')][string]$Segredo   # omitido: reaproveita o do web.config ou gera um
)
$ErrorActionPreference = 'Stop'
$apphost = 'MACHINE/WEBROOT/APPHOST'
$raiz = 'C:\inetpub\flow'
$Thumbprint = $Thumbprint -replace '\s', ''
$passo = 0

function Passo($texto) { $script:passo++; Write-Host "`n[$script:passo] $texto" -ForegroundColor Cyan }
function Validar([bool]$condicao, $ok, $erro) {
    if ($WhatIfPreference) { Write-Host "    (WhatIf) não validado: $ok" -ForegroundColor DarkGray; return }
    if (-not $condicao) { throw "FALHOU: $erro" }
    Write-Host "    OK  $ok" -ForegroundColor Green
}
function Ler($filtro, $nome, $local = $null) {
    $p = @{ PSPath = $apphost; Filter = $filtro; Name = $nome }
    if ($local) { $p.Location = $local }
    $v = Get-WebConfigurationProperty @p
    if ($null -ne $v -and $v.PSObject.Properties['Value']) { $v.Value } else { $v }
}
# Só Administrators e SYSTEM (por SID: o nome muda com o idioma do Windows) e a conta de serviço; ficheiros com o segredo
function Restringir($ficheiro) {
    if ($WhatIfPreference) { return }
    icacls $ficheiro /inheritance:r /grant:r '*S-1-5-32-544:F' '*S-1-5-18:F' "${ContaPool}:RX" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "FALHOU: icacls em $ficheiro (a conta $ContaPool existe?)" }
}

Passo 'Funções do Windows: IIS e Windows Authentication'
if (Get-Command Install-WindowsFeature -ErrorAction SilentlyContinue) {
    Install-WindowsFeature Web-Server, Web-Windows-Auth | Out-Null
    $falta = @(Get-WindowsFeature Web-Server, Web-Windows-Auth | Where-Object { -not $_.Installed })
} else {   # Windows cliente: só para ensaios
    Enable-WindowsOptionalFeature -Online -All -NoRestart -FeatureName IIS-WebServerRole, IIS-WindowsAuthentication | Out-Null
    $falta = @('IIS-WebServerRole', 'IIS-WindowsAuthentication' | Where-Object { (Get-WindowsOptionalFeature -Online -FeatureName $_).State -ne 'Enabled' })
}
Validar ($falta.Count -eq 0) 'IIS e Windows Authentication instalados' "faltam funções: $($falta -join ', ')"
if (-not (Get-Module -ListAvailable WebAdministration)) {
    if ($WhatIfPreference) { Write-Host 'O IIS ainda não está instalado: o -WhatIf pára aqui. Corra sem -WhatIf para o instalar.'; return }
    throw 'FALHOU: o módulo WebAdministration não existe (reinicie a sessão do PowerShell depois de instalar o IIS).'
}
Import-Module WebAdministration

Passo 'URL Rewrite e Application Request Routing'
$modulos = (Get-WebGlobalModule).Name
Validar ($modulos -contains 'RewriteModule') 'URL Rewrite instalado' 'falta o URL Rewrite 2.1 (https://www.iis.net/downloads/microsoft/url-rewrite)'
Validar ($modulos -contains 'ApplicationRequestRouting') 'ARR instalado' 'falta o Application Request Routing 3.0 (https://www.iis.net/downloads/microsoft/application-request-routing)'

Passo "Certificado $Thumbprint"
$cert = Get-Item "Cert:\LocalMachine\My\$Thumbprint" -ErrorAction SilentlyContinue
Validar ($null -ne $cert) 'certificado encontrado em LocalMachine\My' 'certificado não encontrado (confirme o thumbprint)'
Validar ($cert.HasPrivateKey) 'tem chave privada' 'o certificado não tem chave privada'
Validar ($cert.NotAfter -gt (Get-Date).AddDays(30)) "válido até $($cert.NotAfter.ToString('dd-MM-yyyy'))" 'o certificado expira em menos de 30 dias ou já expirou'
Validar (@($cert.DnsNameList.Unicode | Where-Object { $Nome -like $_ }).Count -gt 0) "cobre $Nome" "o certificado não cobre $Nome (nomes: $($cert.DnsNameList.Unicode -join ', '))"

Passo "DNS de $Nome"
if (Resolve-DnsName $Nome -ErrorAction SilentlyContinue) { Write-Host '    OK  resolve' -ForegroundColor Green }
else { Write-Warning "$Nome não resolve. Sem registo DNS (A/CNAME, nunca IP) o Kerberos não funciona." }

Passo "Pacote em $Pasta e .NET 10"
Validar (Test-Path (Join-Path $Pasta 'Vendas.Agente.exe')) 'Vendas.Agente.exe presente' "copie pacote\Flow para $Pasta"
Validar (Test-Path (Join-Path $Pasta 'servidor\Vendas.Servidor.dll')) 'servidor\Vendas.Servidor.dll presente' "falta a subpasta servidor em $Pasta"
Validar ([bool](Get-Command dotnet -ErrorAction SilentlyContinue) -and ((dotnet --list-runtimes) -match '^Microsoft\.NETCore\.App 10\.')) '.NET 10 Runtime instalado' 'falta o .NET 10 Runtime (não o Hosting Bundle)'

Passo 'Segredo partilhado e Iniciar-Flow.cmd'
$webConfig = Join-Path $raiz 'web.config'
if (-not $Segredo -and (Test-Path $webConfig)) {
    $Segredo = [regex]::Match((Get-Content $webConfig -Raw), 'HTTP_X_PROXY_SECRET" value="([0-9a-f]{32,})"').Groups[1].Value
}
if (-not $Segredo) {
    $bytes = New-Object byte[] 32
    [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    $Segredo = ($bytes | ForEach-Object { $_.ToString('x2') }) -join ''
}
$cmd = Join-Path $Pasta 'Iniciar-Flow.cmd'
if (-not (Test-Path $cmd)) { Copy-Item (Join-Path $PSScriptRoot 'Iniciar-Flow.cmd') $cmd }
$resumo = Join-Path $Pasta 'Enviar-Resumo.cmd'   # sem segredo: lê o VENDAS_SQL do Iniciar-Flow.cmd
if (-not (Test-Path $resumo)) { Copy-Item (Join-Path $PSScriptRoot 'Enviar-Resumo.cmd') $resumo }
if (-not $WhatIfPreference) {
    $texto = (Get-Content $cmd -Raw) -replace '(?m)^set VENDAS_PROXY_SEGREDO=.*$', "set VENDAS_PROXY_SEGREDO=$Segredo" `
        -replace '(?m)^set VENDAS_ENDERECO_PUBLICO=.*$', "set VENDAS_ENDERECO_PUBLICO=https://$Nome/" `
        -replace '(?m)^set VENDAS_ENDERECO=.*$', "set VENDAS_ENDERECO=http://localhost:$Porta/"
    Set-Content $cmd $texto -Encoding ASCII -NoNewline
    Restringir $cmd
    if ($texto -match 'VENDAS_SQL=PREENCHER') { Write-Warning "Edite $cmd e preencha a linha VENDAS_SQL (o segredo e o endereço já estão certos)." }
}
Validar ((Get-Content $cmd -Raw).Contains("VENDAS_PROXY_SEGREDO=$Segredo")) 'segredo gravado no Iniciar-Flow.cmd' 'o segredo não ficou no Iniciar-Flow.cmd'
Validar ((Get-Acl $cmd).Access.Count -eq 3) 'Iniciar-Flow.cmd só legível por Administrators, SYSTEM e a conta de serviço' 'permissões do Iniciar-Flow.cmd inesperadas'

Passo 'Proxy do ARR (timeout 10 min, sem buffer, Host não preservado)'
$proxy = 'system.webServer/proxy'
Set-WebConfigurationProperty -PSPath $apphost -Filter $proxy -Name enabled -Value $true
Set-WebConfigurationProperty -PSPath $apphost -Filter $proxy -Name timeout -Value '00:10:00'
Set-WebConfigurationProperty -PSPath $apphost -Filter $proxy -Name responseBufferLimit -Value 0
Set-WebConfigurationProperty -PSPath $apphost -Filter $proxy -Name preserveHostHeader -Value $false
Set-WebConfigurationProperty -PSPath $apphost -Filter $proxy -Name reverseRewriteHostInResponseHeaders -Value $false
Validar ((Ler $proxy enabled) -eq $true) 'proxy ativo' 'o proxy do ARR não ficou ativo'
Validar (([TimeSpan]"$(Ler $proxy timeout)").TotalSeconds -eq 600) 'timeout de 600 s' 'timeout do proxy errado'
Validar ((Ler $proxy responseBufferLimit) -eq 0) 'responseBufferLimit = 0 (o chat aparece à medida que é gerado)' 'responseBufferLimit diferente de 0'
Validar ((Ler $proxy preserveHostHeader) -eq $false) 'Host original não preservado (senão o HttpListener dá 400)' 'preserveHostHeader ficou ligado'

Passo 'Variáveis de servidor permitidas'
foreach ($v in 'HTTP_X_REMOTE_USER', 'HTTP_X_PROXY_SECRET') {
    if (-not (Ler "system.webServer/rewrite/allowedServerVariables/add[@name='$v']" name)) {
        Add-WebConfigurationProperty -PSPath $apphost -Filter 'system.webServer/rewrite/allowedServerVariables' -Name '.' -Value @{ name = $v }
    }
    Validar ([bool](Ler "system.webServer/rewrite/allowedServerVariables/add[@name='$v']" name)) "$v permitida" "$v não ficou permitida"
}

Passo "Pool '$Site' (sem código gerido, conta $ContaPool)"
if (-not (Test-Path "IIS:\AppPools\$Site")) {
    if (-not $WhatIfPreference) { $cred = Get-Credential -UserName $ContaPool -Message "Palavra-passe de $ContaPool" }
    New-WebAppPool -Name $Site | Out-Null
    if ($cred) {
        Set-ItemProperty "IIS:\AppPools\$Site" -Name processModel -Value @{ identityType = 'SpecificUser'; userName = $cred.UserName; password = $cred.GetNetworkCredential().Password }
    }
}
Set-ItemProperty "IIS:\AppPools\$Site" -Name managedRuntimeVersion -Value ''
$pool = Get-Item "IIS:\AppPools\$Site" -ErrorAction SilentlyContinue
Validar ($pool.managedRuntimeVersion -eq '') 'sem código gerido' 'o pool tem código gerido'
Validar ($pool.processModel.identityType -eq 'SpecificUser' -and $pool.processModel.userName -ieq $ContaPool) "corre como $ContaPool" "o pool existente corre como '$($pool.processModel.userName)', não $ContaPool"

Passo "Site '$Site' em https://$Nome"
if (-not (Test-Path $raiz)) { New-Item -ItemType Directory $raiz | Out-Null }
$ligado = Get-Item 'IIS:\SslBindings\0.0.0.0!443' -ErrorAction SilentlyContinue
Validar (-not $ligado -or $ligado.Thumbprint -eq $Thumbprint -or (Get-Website -Name $Site)) 'porta 443 livre ou com o mesmo certificado' "o 443 já tem outro certificado ($($ligado.Thumbprint)): ligue o certificado por SNI à mão"
if (-not (Get-Website -Name $Site)) {
    New-Website -Name $Site -PhysicalPath $raiz -ApplicationPool $Site -HostHeader $Nome -Port 443 -Ssl | Out-Null
}
if (-not (Test-Path 'IIS:\SslBindings\0.0.0.0!443')) { (Get-WebBinding -Name $Site -Protocol https).AddSslCertificate($Thumbprint, 'My') }
$ligacao = Get-WebBinding -Name $Site -Protocol https
Validar ($ligacao.bindingInformation -like "*:443:$Nome") "binding https $Nome:443" 'o site não tem o binding https esperado'
Validar ((Get-Item 'IIS:\SslBindings\0.0.0.0!443').Thumbprint -eq $Thumbprint) 'certificado ligado ao 443' 'o certificado não ficou ligado ao 443'

Passo 'Autenticação: Windows ligada, Anónima desligada (no applicationHost, o web.config não a pode alterar)'
$auth = 'system.webServer/security/authentication'
Set-WebConfigurationProperty -PSPath $apphost -Location $Site -Filter "$auth/windowsAuthentication" -Name enabled -Value $true
Set-WebConfigurationProperty -PSPath $apphost -Location $Site -Filter "$auth/windowsAuthentication" -Name useKernelMode -Value $true
Set-WebConfigurationProperty -PSPath $apphost -Location $Site -Filter "$auth/windowsAuthentication" -Name useAppPoolCredentials -Value $true
Set-WebConfigurationProperty -PSPath $apphost -Location $Site -Filter "$auth/anonymousAuthentication" -Name enabled -Value $false
Validar ((Ler "$auth/windowsAuthentication" enabled $Site) -eq $true) 'Windows Authentication ligada' 'Windows Authentication não ficou ligada'
Validar ((Ler "$auth/windowsAuthentication" useAppPoolCredentials $Site) -eq $true) 'modo kernel com credenciais do pool (Kerberos com o SPN na conta do pool)' 'useAppPoolCredentials não ficou ligado'
Validar ((Ler "$auth/anonymousAuthentication" enabled $Site) -eq $false) 'Anónima desligada' 'a autenticação anónima continua ligada'

Passo 'web.config do site (regra de rewrite e segredo)'
if (-not $WhatIfPreference) {
    $xml = (Get-Content (Join-Path $PSScriptRoot 'web.config') -Raw).Replace('SEGREDO_PARTILHADO', $Segredo).Replace('http://localhost:5080/', "http://localhost:$Porta/")
    Set-Content $webConfig $xml -Encoding UTF8 -NoNewline
    Restringir $webConfig
}
$lido = $null
if (Test-Path $webConfig) { try { $lido = [xml](Get-Content $webConfig -Raw) } catch { } }
Validar ($null -ne $lido) 'web.config é XML válido' 'o web.config não é XML válido'
Validar (-not $WhatIfPreference -and $lido.OuterXml.Contains($Segredo) -and $lido.OuterXml.Contains("localhost:$Porta/")) 'segredo e porta iguais aos do Iniciar-Flow.cmd' 'segredo ou porta em falta no web.config'
Validar ((Get-Acl $webConfig).Access.Count -eq 3) 'web.config só legível por Administrators, SYSTEM e a conta de serviço' 'permissões do web.config inesperadas'
Validar ((Get-WebConfigurationProperty -PSPath "IIS:\Sites\$Site" -Filter 'system.webServer/rewrite/rules/rule' -Name name) -eq 'Flow') 'o IIS lê a regra "Flow"' 'o IIS não lê a regra do web.config (URL Rewrite instalado?)'

Passo 'Arrancar pool e site'
if ((Get-WebAppPoolState $Site).Value -ne 'Started') { Start-WebAppPool $Site }
if ((Get-WebsiteState $Site).Value -ne 'Started') { Start-Website $Site }
Validar ((Get-WebAppPoolState $Site).Value -eq 'Started') 'pool iniciado' 'o pool não arrancou (palavra-passe da conta errada? veja o Visualizador de Eventos)'
Validar ((Get-WebsiteState $Site).Value -eq 'Started') 'site iniciado' 'o site não arrancou (porta ou binding em conflito?)'

Passo 'Teste de fumo a partir deste servidor (informativo)'
if (-not $WhatIfPreference) {
    try { $estado = [int](Invoke-WebRequest "https://$Nome/" -UseDefaultCredentials -UseBasicParsing -TimeoutSec 30).StatusCode }
    catch { $estado = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { $_.Exception.Message } }
    $leitura = switch ($estado) {
        200 { 'o site responde e o agente também' }
        403 { 'o IIS e o agente respondem; este utilizador não está em perfis.json' }
        502 { 'o IIS responde mas o agente não: arranque a tarefa "Flow (agente)"' }
        401 { 'autenticação recusada: falta o SPN, ou é o loopback check ao testar no próprio servidor (teste a partir de um posto)' }
        default { 'inesperado, ver docs\IIS.md secção 8' }
    }
    Write-Host "    $estado  $leitura"
}

Write-Host "`nIIS pronto. Faltam (fora deste script):" -ForegroundColor Yellow
Write-Host "  1. SPN, num controlador de domínio:  setspn -S HTTP/$Nome $ContaPool   e   setspn -X (duplicados)"
Write-Host "  2. Tarefa agendada 'Flow (agente)' com $cmd (docs\IIS.md passo 3), e perfis.json."
Write-Host "  3. Firewall: a porta $Porta não pode estar aberta. Depois: .\Verificar-Flow.ps1 -Nome $Nome -Login <DOMINIO\utilizador>"
