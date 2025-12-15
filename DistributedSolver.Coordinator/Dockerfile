FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5000
ENV ASPNETCORE_URLS=http://0.0.0.0:5000

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["DistributedSolver.Coordinator/DistributedSolver.Coordinator.csproj", "DistributedSolver.Coordinator/"]
COPY ["DistributedSolver.Core/DistributedSolver.Core.csproj", "DistributedSolver.Core/"]
RUN dotnet restore "DistributedSolver.Coordinator/DistributedSolver.Coordinator.csproj"
COPY . .
WORKDIR "/src/DistributedSolver.Coordinator"
RUN dotnet build "DistributedSolver.Coordinator.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DistributedSolver.Coordinator.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DistributedSolver.Coordinator.dll", "5000"]

