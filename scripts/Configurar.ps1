# Prepara este PC e este utilizador Windows para abrir o Flow. Corre-se uma vez, dentro da pasta do pacote.
# Só para testes da equipa de desenvolvimento: neste modo não há login nem perfis.
$ErrorActionPreference = 'Stop'

$runtimes = if (Get-Command dotnet -ErrorAction SilentlyContinue) { dotnet --list-runtimes } else { @() }
if (-not ($runtimes -match '^Microsoft\.NETCore\.App 10\.')) {
    Write-Host 'Falta o .NET 10 Runtime. Instale-o a partir de https://dotnet.microsoft.com/download/dotnet/10.0 e corra este script de novo.'
    exit 1
}

function Confirmar($pergunta) { (Read-Host "$pergunta (s/N)") -match '^[sS]$' }

function Pedir($pergunta, $omissao) {
    $resposta = (Read-Host "$pergunta [$omissao]").Trim()
    if (-not $resposta) { $resposta = $omissao }
    if ($resposta.Contains(';')) { throw "O valor não pode ter ';'." }   # partiria a connection string
    $resposta
}

# Ligação ao SQL Server, sem a palavra-passe
if (-not [Environment]::GetEnvironmentVariable('VENDAS_SQL', 'User') -or (Confirmar 'Já existe uma ligação configurada. Substituir?')) {
    $servidor = Pedir 'Servidor SQL (servidor,porta)' ''
    if (-not $servidor) { throw 'Indique o servidor SQL.' }
    $base = Pedir 'Base de dados' 'IAVSGIX'
    $login = Pedir 'Login SQL' 'mcpserver'
    [Environment]::SetEnvironmentVariable('VENDAS_SQL', "Server=tcp:$servidor;Database=$base;User Id=$login;Encrypt=True;TrustServerCertificate=True", 'User')
}

# Palavra-passe cifrada com DPAPI: só este utilizador, neste PC, a consegue ler
$pastaDados = Join-Path $env:LOCALAPPDATA 'Vendas'
$ficheiroPwd = Join-Path $pastaDados 'sql.pwd'
if (-not (Test-Path $ficheiroPwd) -or (Confirmar 'Já existe uma palavra-passe guardada. Substituir?')) {
    New-Item -ItemType Directory -Force $pastaDados | Out-Null
    Read-Host 'Palavra-passe SQL' -AsSecureString | ConvertFrom-SecureString | Set-Content $ficheiroPwd
}

$atalho = (New-Object -ComObject WScript.Shell).CreateShortcut((Join-Path ([Environment]::GetFolderPath('Desktop')) 'Inforap Flow.lnk'))
$atalho.TargetPath = Join-Path $PSScriptRoot 'Vendas.Agente.exe'
$atalho.Arguments = '--web'
$atalho.WorkingDirectory = $PSScriptRoot
$atalho.Save()

Write-Host 'Pronto. Abra o Flow com o atalho "Inforap Flow" no ambiente de trabalho.'
