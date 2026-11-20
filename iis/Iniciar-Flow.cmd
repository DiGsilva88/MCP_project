@echo off
rem Arranca o agente em modo proxy (atras do IIS). Corre como tarefa agendada da conta de servico.
rem O Instalar-IIS.ps1 copia este modelo para a pasta do Flow e preenche o segredo e o endereco publico.
rem Tem o segredo: so Administrators, SYSTEM e a conta de servico o podem ler (o Instalar-IIS.ps1 faz isso).
cd /d "%~dp0"

rem VENDAS_SQL sem a palavra-passe (vem do sql.pwd, cifrado com a conta que corre a tarefa)
set VENDAS_SQL=PREENCHER: Server=tcp:servidor,1433;Database=IAVSGIX;User Id=mcpserver;Encrypt=True;TrustServerCertificate=True
set VENDAS_PROXY_SEGREDO=PREENCHER
rem Demonstracao sem Windows Auth (Instalar-IIS.ps1 -LinkAssinado): segredo que o Visual usa para assinar o link. Vazio = desligado.
set VENDAS_LINK_SEGREDO=
set VENDAS_ENDERECO=http://localhost:5080/
set VENDAS_ENDERECO_PUBLICO=https://flow.inforap.local/

rem Ollama que vai no pacote (ollama\): arranca aqui, so em loopback, se ainda nao estiver a correr. Sem a pasta, usa o que ja existir.
if exist ollama\ollama.exe (
  set OLLAMA_HOST=127.0.0.1:11434
  set OLLAMA_MODELS=%~dp0ollama\modelos
  tasklist /fi "imagename eq ollama.exe" | find /i "ollama.exe" >nul || start "" /b ollama\ollama.exe serve >> ollama.log 2>&1
)

where dotnet >nul 2>&1 || (echo %date% %time% dotnet nao encontrado no PATH da conta >> flow.log & exit /b 1)
if not exist Vendas.Agente.exe (echo %date% %time% Vendas.Agente.exe nao encontrado em %cd% >> flow.log & exit /b 1)

Vendas.Agente.exe --web >> flow.log 2>&1
exit /b %errorlevel%
