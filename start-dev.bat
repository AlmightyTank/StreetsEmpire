@echo off
setlocal

set "ROOT=%~dp0"
set "API_URL=http://localhost:5080"
set "CLIENT_URL=http://localhost:5173"

echo Starting Street Empire development servers...
echo API:    %API_URL%
echo Client: %CLIENT_URL%
echo.

if not exist "%ROOT%Client\node_modules" (
  echo Client dependencies are missing.
  echo Run: cd Client ^&^& npm install
  echo.
  pause
  exit /b 1
)

start "Street Empire API" cmd /k "cd /d ""%ROOT%"" && dotnet run --project Server\StreetEmpire.Api\StreetEmpire.Api.csproj --urls %API_URL%"
start "Street Empire Client" cmd /k "cd /d ""%ROOT%Client"" && npm run dev"

echo Started API and client in separate windows.
echo Open %CLIENT_URL% after Vite finishes starting.
echo.
pause
