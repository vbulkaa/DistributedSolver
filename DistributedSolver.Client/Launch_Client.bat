@echo off
echo Запуск клиента Distributed Solver...
echo.
echo Убедитесь, что кластер docker-compose активирован:
echo   docker-compose up --build
echo.
pause
dotnet run --project DistributedSolver.Client.csproj
pause

