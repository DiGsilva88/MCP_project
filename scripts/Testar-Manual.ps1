# Testes manuais do Flow, a partir do terminal do Visual Studio (Ver > Terminal) ou de qualquer PowerShell.
# Fala diretamente com o servidor MCP (stdio, JSON-RPC), sem Ollama nem modelo: serve para ver o que cada tool
# devolve, quanto tempo demora e se alguma resposta deixa escapar detalhes técnicos.
#
#   .\scripts\Testar-Manual.ps1 -Demo        dados fictícios (RepoDemo), não precisa de VENDAS_SQL
#   .\scripts\Testar-Manual.ps1              base de dados real (VENDAS_SQL + sql.pwd, como no README)
#   .\scripts\Testar-Manual.ps1 -Cliente 1003 -Documento "N 10/2026/1034675"   casos com um cliente/documento real
#
# Se o PowerShell bloquear o script: powershell -ExecutionPolicy Bypass -File scripts\Testar-Manual.ps1 -Demo
param(
    [switch]$Demo,
    [string]$Cliente,
    [string]$Documento,
    [switch]$IncluirSensivel,   # inclui consultar_sensivel na bateria (só mostra o tamanho da resposta)
    [switch]$SemCompilar,
    [int]$TimeoutSegundos = 60
)

$ErrorActionPreference = 'Stop'
$raiz = Split-Path $PSScriptRoot -Parent
$projetoServidor = Join-Path $raiz 'src\Vendas.Servidor'
$projetoAgente = Join-Path $raiz 'src\Vendas.Agente'
$dll = Join-Path $projetoServidor 'bin\Debug\net10.0\Vendas.Servidor.dll'

# Palavras que nunca deviam aparecer numa resposta (PoliticasSeguranca: nada de SQL, views, exceções).
$padraoFuga = 'Exception|ViewMCP|SqlClient|SELECT |Login failed|stack trace|   at Vendas\.|connection string'

# O $env: fica no terminal depois do script: sem repor, o próximo "dotnet run" do agente abria o demo.
$demoAntes = $env:VENDAS_DEMO
if ($Demo) { $env:VENDAS_DEMO = '1' }
elseif (-not $env:VENDAS_SQL) {
    Write-Host 'VENDAS_SQL não está definida. Use -Demo para dados fictícios, ou defina a ligação (ver README).' -ForegroundColor Yellow
    exit 1
}
if (-not $Cliente -and $Demo) { $Cliente = '1001' }

# ------------------------------------------------------------------ servidor MCP (processo filho, stdio)

$script:Processo = $null
$script:Escritor = $null
$script:Leitura = $null
$script:ProximoId = 0
$script:Tools = @()

function Iniciar-Servidor {
    if (-not $SemCompilar) {
        Write-Host 'A compilar o servidor...' -ForegroundColor DarkGray
        dotnet build $projetoServidor -c Debug --nologo -v q | Out-Host
        if ($LASTEXITCODE -ne 0) { throw 'A compilação falhou (ver erros acima).' }
    }
    if (-not (Test-Path $dll)) { throw "Não encontrei $dll. Corra sem -SemCompilar." }
    # Corre uma cópia: assim o dotnet test / dotnet run do menu podem recompilar sem a dll estar bloqueada.
    $copia = Join-Path $env:TEMP "flow-testes-$PID-$([DateTime]::Now.Ticks)"
    Copy-Item (Split-Path $dll -Parent) $copia -Recurse

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = 'dotnet'
    $psi.Arguments = "`"$(Join-Path $copia 'Vendas.Servidor.dll')`""
    $psi.UseShellExecute = $false
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $false          # avisos e erros do servidor aparecem diretamente no terminal
    $psi.StandardOutputEncoding = New-Object System.Text.UTF8Encoding($false)
    $psi.EnvironmentVariables['Logging__LogLevel__Default'] = 'Warning'   # sem o ruído do arranque do host

    $script:Processo = [System.Diagnostics.Process]::Start($psi)
    # stdin em UTF-8 sem BOM (o Windows PowerShell 5.1 usaria a página de código da consola)
    $script:Escritor = New-Object System.IO.StreamWriter($script:Processo.StandardInput.BaseStream, (New-Object System.Text.UTF8Encoding($false)))
    $script:Escritor.AutoFlush = $true

    $null = Enviar 'initialize' @{
        protocolVersion = '2025-06-18'
        capabilities    = @{}
        clientInfo      = @{ name = 'testes-manuais'; version = '1.0' }
    }
    Enviar 'notifications/initialized' $null -Notificacao
    $script:Tools = @((Enviar 'tools/list' @{}).tools)
    $modo = if ($Demo) { 'DEMO (dados fictícios)' } else { 'base de dados real' }
    Write-Host "Servidor ligado — $($script:Tools.Count) tools — $modo" -ForegroundColor Green
}

function Parar-Servidor {
    if ($script:Processo -and -not $script:Processo.HasExited) {
        $script:Escritor.Close()                 # fechar o stdin faz o servidor sair sozinho
        if (-not $script:Processo.WaitForExit(3000)) { $script:Processo.Kill() }
    }
}

function Enviar([string]$metodo, $parametros, [switch]$Notificacao) {
    $msg = @{ jsonrpc = '2.0'; method = $metodo }
    if ($null -ne $parametros) { $msg.params = $parametros }
    if (-not $Notificacao) { $script:ProximoId++; $msg.id = $script:ProximoId }
    $script:Escritor.WriteLine(($msg | ConvertTo-Json -Compress -Depth 20))
    if ($Notificacao) { return }

    $fim = [DateTime]::UtcNow.AddSeconds($TimeoutSegundos)
    while ($true) {
        # A leitura pendente sobrevive a um timeout: pedir outra no mesmo stream daria erro.
        if ($null -eq $script:Leitura) { $script:Leitura = $script:Processo.StandardOutput.ReadLineAsync() }
        $falta = [int][Math]::Max(0, ($fim - [DateTime]::UtcNow).TotalMilliseconds)
        if (-not $script:Leitura.Wait($falta)) { throw "Sem resposta do servidor em $TimeoutSegundos s ($metodo)." }
        $linha = $script:Leitura.Result
        $script:Leitura = $null
        if ($null -eq $linha) { throw 'O servidor terminou (ver mensagens acima).' }
        if (-not $linha.Trim()) { continue }
        $resp = $linha | ConvertFrom-Json
        if ($resp.id -ne $msg.id) { continue }  # notificações do servidor
        if ($resp.error) { throw "Erro JSON-RPC $($resp.error.code): $($resp.error.message)" }
        return $resp.result
    }
}

function Chamar-Tool([string]$nome, [hashtable]$argumentos) {
    $relogio = [System.Diagnostics.Stopwatch]::StartNew()
    $r = Enviar 'tools/call' @{ name = $nome; arguments = $argumentos }
    $relogio.Stop()
    $texto = (@($r.content) | Where-Object { $_.type -eq 'text' } | ForEach-Object { $_.text }) -join "`n"
    [pscustomobject]@{ Erro = [bool]$r.isError; Texto = [string]$texto; Ms = $relogio.ElapsedMilliseconds }
}

function Mostrar-Inicio([string]$texto, [int]$linhas = 4) {
    $partes = $texto -split "`r?`n" | Select-Object -First $linhas
    foreach ($p in $partes) {
        if ($p.Length -gt 140) { $p = $p.Substring(0, 140) + '…' }
        Write-Host "      $p" -ForegroundColor DarkGray
    }
}

# ------------------------------------------------------------------ 1. bateria de testes

function Correr-Bateria {
    $ano = (Get-Date).Year
    $casos = @(
        @{ Nome = 'Contar clientes por zona';           Tool = 'consultar';        Args = @{ coluna = 'Zona'; contar = $true; limite = 5 } }
        @{ Nome = 'Listar clientes (pág. 2)';           Tool = 'consultar';        Args = @{ limite = 5; pagina = 2 } }
        @{ Nome = 'Top 5 clientes do ano';              Tool = 'consultar_vendas'; Args = @{ agrupar = 'Cliente'; periodo = 'AnoAtual'; limite = 5 } }
        @{ Nome = 'Vendas por mês (12 meses)';          Tool = 'consultar_vendas'; Args = @{ agrupar = 'Mes'; periodo = 'Ultimos12Meses' } }
        @{ Nome = 'Artigos vs. ano anterior';           Tool = 'consultar_vendas'; Args = @{ agrupar = 'Artigo'; compararAnoAnterior = $true; limite = 5 } }
        @{ Nome = 'Artigo mais vendido por distrito';   Tool = 'consultar_vendas'; Args = @{ agrupar = 'Artigo'; emCada = 'Distrito' } }
        @{ Nome = 'Clientes sem compras (90 dias)';     Tool = 'consultar_vendas'; Args = @{ agrupar = 'Cliente'; periodo = 'Ultimos90Dias'; semVendas = $true; limite = 5 } }
        @{ Nome = 'Vendas do 1.º trimestre';            Tool = 'consultar_vendas'; Args = @{ agrupar = 'Zona'; trimestre = 1 } }
        @{ Nome = 'Documentos (painel Faturação)';      Tool = 'consultar_documentos'; Args = @{ periodo = 'AnoAtual'; limite = 5 } }
        @{ Nome = 'Notas de crédito';                   Tool = 'consultar_creditos';   Args = @{ periodo = 'AnoAtual'; limite = 5 } }
        @{ Nome = 'Carteira de clientes';               Tool = 'consultar_carteira';   Args = @{ periodo = 'AnoAtual'; limite = 10 } }
    )
    if ($Cliente) {
        $casos += @{ Nome = "Ficha do cliente $Cliente";     Tool = 'consultar';        Args = @{ cliente = $Cliente } }
        $casos += @{ Nome = "O que compra o cliente $Cliente"; Tool = 'consultar_vendas'; Args = @{ agrupar = 'Artigo'; cliente = $Cliente; limite = 5 } }
    }
    if ($Documento) {
        $casos += @{ Nome = "Documento $Documento"; Tool = 'consultar_documento'; Args = @{ documento = $Documento } }
    }
    if ($IncluirSensivel) {
        $casos += @{ Nome = 'Dados sensíveis (só tamanho)'; Tool = 'consultar_sensivel'; Args = @{ dado = 'Email'; limite = 3 }; Sensivel = $true }
    }
    # Casos que devem ser recusados com uma mensagem simples, sem detalhes técnicos.
    $casos += @(
        @{ Nome = 'Limite acima de 100';             Tool = 'consultar_vendas';    Args = @{ agrupar = 'Cliente'; limite = 500 }; Negativo = $true }
        @{ Nome = 'Ano no futuro';                   Tool = 'consultar_vendas';    Args = @{ agrupar = 'Cliente'; ano = $ano + 1 }; Negativo = $true }
        @{ Nome = 'Trimestre combinado com mês';     Tool = 'consultar_vendas';    Args = @{ agrupar = 'Cliente'; trimestre = 2; mes = 5 }; Negativo = $true }
        @{ Nome = 'Tentativa de SQL injection';      Tool = 'consultar';           Args = @{ coluna = 'Zona'; valor = "x'; DROP TABLE t; --" }; Negativo = $true }
        @{ Nome = 'Documento com formato inválido';  Tool = 'consultar_documento'; Args = @{ documento = 'abc' }; Negativo = $true }
        @{ Nome = 'Coluna que não existe (sem a validação do agente)'; Tool = 'consultar'; Args = @{ coluna = 'Zonaa' }; Negativo = $true }
    )

    $ok = 0; $falhas = 0
    foreach ($c in $casos) {
        if (-not ($script:Tools | Where-Object name -eq $c.Tool)) {
            Write-Host "  [SALTADO] $($c.Nome) — a tool $($c.Tool) não está registada" -ForegroundColor Yellow
            continue
        }
        try { $r = Chamar-Tool $c.Tool $c.Args }
        catch { $r = [pscustomobject]@{ Erro = $true; Texto = "$_"; Ms = 0 } }

        $fuga = $r.Texto -match $padraoFuga
        $falhouConsulta = $r.Texto -like 'Não foi possível consultar*'
        $vazio = $r.Texto.Trim() -eq ''
        if ($c.Negativo) { $passou = -not $fuga -and -not $vazio }
        else { $passou = -not $r.Erro -and -not $fuga -and -not $falhouConsulta -and -not $vazio }

        if ($passou) { $ok++; $cor = 'Green'; $estado = 'OK    ' } else { $falhas++; $cor = 'Red'; $estado = 'FALHOU' }
        Write-Host ("  [{0}] {1} ({2} ms)" -f $estado, $c.Nome, $r.Ms) -ForegroundColor $cor
        if ($fuga) { Write-Host '      ↳ a resposta tem detalhes técnicos (SQL, views ou exceções)' -ForegroundColor Red }
        if ($falhouConsulta) { Write-Host '      ↳ a consulta falhou no servidor: ver a mensagem [servidor] acima' -ForegroundColor Red }
        if ($c.Sensivel) { Write-Host "      ($($r.Texto.Length) caracteres — conteúdo não mostrado)" -ForegroundColor DarkGray }
        else { Mostrar-Inicio $r.Texto }
    }
    $cor = if ($falhas) { 'Red' } else { 'Green' }
    Write-Host "`nResultado: $ok OK, $falhas com falhas, de $($casos.Count) casos." -ForegroundColor $cor
}

# ------------------------------------------------------------------ 2. chamada livre a uma tool

function Tipo-Parametro($schema) {
    $tipos = @($schema.type) + @($schema.anyOf | ForEach-Object { $_.type }) | Where-Object { $_ }
    if ($tipos -contains 'integer') { return 'integer' }
    if ($tipos -contains 'number') { return 'number' }
    if ($tipos -contains 'boolean') { return 'boolean' }
    return 'string'
}

function Chamada-Livre {
    for ($i = 0; $i -lt $script:Tools.Count; $i++) { Write-Host ("  {0}. {1}" -f ($i + 1), $script:Tools[$i].name) }
    $n = Read-Host 'Número da tool'
    if (-not ($n -as [int]) -or [int]$n -lt 1 -or [int]$n -gt $script:Tools.Count) { return }
    $tool = $script:Tools[[int]$n - 1]

    $props = @{}
    if ($tool.inputSchema.properties) {
        foreach ($p in $tool.inputSchema.properties.PSObject.Properties) {
            $props[$p.Name] = Tipo-Parametro $p.Value
            $valores = @($p.Value.enum) + @($p.Value.anyOf | ForEach-Object { $_.enum }) | Where-Object { $_ }
            $extra = if ($valores) { ': ' + ($valores -join ', ') } else { '' }
            Write-Host ("    {0} ({1}){2}" -f $p.Name, $props[$p.Name], $extra) -ForegroundColor DarkGray
        }
    }
    Write-Host '  Argumentos como chave=valor; chave=valor  (ex.: agrupar=Cliente; limite=5). Enter = nenhum.'
    $linha = Read-Host 'Argumentos'

    $argumentos = @{}
    foreach ($par in ($linha -split ';')) {
        if (-not $par.Trim()) { continue }
        $chave, $valor = $par -split '=', 2
        $chave = $chave.Trim(); $valor = "$valor".Trim()
        if (-not $props.ContainsKey($chave)) { Write-Host "  Aviso: '$chave' não é um parâmetro desta tool (vai na mesma)." -ForegroundColor Yellow }
        switch ($props[$chave]) {
            'integer' { $argumentos[$chave] = [int]$valor }
            'number'  { $argumentos[$chave] = [double]$valor }
            'boolean' { $argumentos[$chave] = $valor -in 'true', 'sim', '1' }
            default   { $argumentos[$chave] = $valor }
        }
    }

    $r = Chamar-Tool $tool.name $argumentos
    $cor = if ($r.Erro) { 'Red' } else { 'Green' }
    Write-Host "`n--- $($tool.name) — $($r.Ms) ms$(if ($r.Erro) { ' — isError' }) ---" -ForegroundColor $cor
    if ($tool.name -eq 'consultar_sensivel') { Write-Host "($($r.Texto.Length) caracteres — conteúdo não mostrado)" }
    else { Write-Host $r.Texto }
    if ($r.Texto -match $padraoFuga) { Write-Host 'Atenção: a resposta tem detalhes técnicos.' -ForegroundColor Red }
}

# ------------------------------------------------------------------ menu

# Na mesma consola, para o agente poder ler o teclado. O VENDAS_DEMO deste terminal passa para o agente.
function Correr-Dotnet([string]$argumentos) {
    Start-Process dotnet -ArgumentList $argumentos -NoNewWindow -Wait
}

try {
    Iniciar-Servidor
    while ($true) {
        Write-Host ''
        Write-Host '  1. Bateria de testes (todas as tools + casos de erro)'
        Write-Host '  2. Chamar uma tool à mão'
        Write-Host '  3. Listar tools'
        Write-Host '  4. Testes automáticos (dotnet test, sem integração)'
        Write-Host '  5. Abrir o agente na consola (precisa do Ollama)'
        Write-Host '  6. Abrir o painel no browser (--web)'
        Write-Host '  0. Sair'
        $opcao = Read-Host 'Opção'
        try {
            switch ($opcao) {
                '1' { Correr-Bateria }
                '2' { Chamada-Livre }
                '3' { $script:Tools | ForEach-Object { Write-Host "  - $($_.name)" } }
                '4' { Correr-Dotnet "test `"$(Join-Path $raiz 'Vendas.slnx')`" --filter `"Categoria!=Integração`"" }
                '5' { Correr-Dotnet "run --project `"$projetoAgente`"" }
                '6' { Correr-Dotnet "run --project `"$projetoAgente`" -- --web" }
                '0' { return }
            }
        }
        catch { Write-Host "Erro: $_" -ForegroundColor Red }
        if ($script:Processo.HasExited) {
            Write-Host 'O servidor parou; a arrancar de novo...' -ForegroundColor Yellow
            $script:Leitura = $null
            Iniciar-Servidor
        }
    }
}
finally {
    Parar-Servidor
    $env:VENDAS_DEMO = $demoAntes   # $null apaga-a
}
