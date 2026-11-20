# Gera em pacote\Flow a pasta a entregar à equipa de desenvolvimento: basta copiá-la e correr Configurar.cmd.
# -Ollama: pasta com o ollama.exe (zip extraído); -Modelos: pasta de modelos (ex.: %USERPROFILE%\.ollama\models). Juntos vão em ollama\ e o Iniciar-Flow.cmd arranca-o.
param([string]$Ollama, [string]$Modelos)
$ErrorActionPreference = 'Stop'
$raiz = Split-Path $PSScriptRoot -Parent
$destino = Join-Path $raiz 'pacote\Flow'

if (Test-Path $destino) { Remove-Item $destino -Recurse -Force }

dotnet publish (Join-Path $raiz 'src\Vendas.Agente') -c Release -o $destino --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Falhou a publicação do agente.' }
dotnet publish (Join-Path $raiz 'src\Vendas.Servidor') -c Release -o (Join-Path $destino 'servidor') --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Falhou a publicação do servidor.' }

Copy-Item (Join-Path $PSScriptRoot 'Configurar.ps1'), (Join-Path $PSScriptRoot 'Configurar.cmd') $destino
Copy-Item (Join-Path $raiz 'iis') (Join-Path $destino 'iis') -Recurse   # instalação em IIS (docs/IIS.md)
if ($Ollama -or $Modelos) {
    if (-not ($Ollama -and $Modelos)) { throw 'Passe -Ollama e -Modelos juntos.' }
    if (-not (Test-Path (Join-Path $Ollama 'ollama.exe'))) { throw "Não encontrei ollama.exe em $Ollama." }
    if (-not (Test-Path (Join-Path $Modelos 'manifests'))) { throw "$Modelos não parece uma pasta de modelos do Ollama (falta manifests)." }
    Copy-Item $Ollama (Join-Path $destino 'ollama') -Recurse
    Copy-Item $Modelos (Join-Path $destino 'ollama\modelos') -Recurse
}
else { Write-Warning 'Sem -Ollama/-Modelos: o pacote não leva o Ollama (tem de estar instalado à parte).' }
Write-Host "Pacote pronto em $destino"
