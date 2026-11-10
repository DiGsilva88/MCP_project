# Gera em pacote\Flow a pasta a entregar à equipa de desenvolvimento: basta copiá-la e correr Configurar.cmd.
$ErrorActionPreference = 'Stop'
$raiz = Split-Path $PSScriptRoot -Parent
$destino = Join-Path $raiz 'pacote\Flow'

if (Test-Path $destino) { Remove-Item $destino -Recurse -Force }

dotnet publish (Join-Path $raiz 'src\Vendas.Agente') -c Release -o $destino --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Falhou a publicação do agente.' }
dotnet publish (Join-Path $raiz 'src\Vendas.Servidor') -c Release -o (Join-Path $destino 'servidor') --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Falhou a publicação do servidor.' }

Copy-Item (Join-Path $PSScriptRoot 'Configurar.ps1'), (Join-Path $PSScriptRoot 'Configurar.cmd') $destino
Write-Host "Pacote pronto em $destino"
