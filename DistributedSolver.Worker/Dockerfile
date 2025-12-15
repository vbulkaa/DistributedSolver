FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 6000
ENV ASPNETCORE_URLS=http://0.0.0.0:6000

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["DistributedSolver.Worker/DistributedSolver.Worker.csproj", "DistributedSolver.Worker/"]
COPY ["DistributedSolver.Core/DistributedSolver.Core.csproj", "DistributedSolver.Core/"]
RUN dotnet restore "DistributedSolver.Worker/DistributedSolver.Worker.csproj"
COPY . .
WORKDIR "/src/DistributedSolver.Worker"
RUN dotnet build "DistributedSolver.Worker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DistributedSolver.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
CMD ["dotnet", "DistributedSolver.Worker.dll", "6000"]

