@echo off
setlocal
dotnet run --project "%~dp0RelayServer.csproj" -- --port 7777
