@echo off
rem Resumo por email (tarefa agendada, segundas). Corre como a MESMA conta do agente: o agendamentos.json e os .pwd (DPAPI) sao dessa conta.
rem O Instalar-IIS.ps1 copia este modelo para a pasta do Flow. Reenviar uma segunda: Enviar-Resumo.cmd --dia 02-11-2026
cd /d "%~dp0"

rem Remetente e login no Office 365 (a palavra-passe esta em %LOCALAPPDATA%\Vendas\smtp.pwd, cifrada). Outro servidor: descomentar.
set VENDAS_SMTP_UTILIZADOR=PREENCHER@inforap.pt
rem set VENDAS_SMTP_HOST=smtp.office365.com
rem set VENDAS_SMTP_PORTA=587

rem A mesma ligacao a base de dados do agente (nao se repete aqui)
for /f "usebackq tokens=1* delims==" %%a in (`findstr /b /c:"set VENDAS_SQL=" "%~dp0Iniciar-Flow.cmd"`) do set "VENDAS_SQL=%%b"

echo %date% %time% resumo por email >> resumo.log
if not defined VENDAS_SQL (echo VENDAS_SQL nao encontrado em Iniciar-Flow.cmd >> resumo.log & exit /b 1)
set VENDAS_SQL | findstr /c:"PREENCHER" >nul && (echo Preencha VENDAS_SQL em Iniciar-Flow.cmd >> resumo.log & exit /b 1)
set VENDAS_SMTP_UTILIZADOR | findstr /c:"PREENCHER" >nul && (echo Preencha VENDAS_SMTP_UTILIZADOR em Enviar-Resumo.cmd >> resumo.log & exit /b 1)
if not exist Vendas.Agente.exe (echo Vendas.Agente.exe nao encontrado em %cd% >> resumo.log & exit /b 1)

Vendas.Agente.exe --enviar-resumo %* >> resumo.log 2>&1
exit /b %errorlevel%
