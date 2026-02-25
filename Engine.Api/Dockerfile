FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates \
    && update-ca-certificates \
    && rm -rf /var/lib/apt/lists/*

COPY ProvisioningChecklistEngine.sln ./
COPY Engine.Core/Engine.Core.csproj Engine.Core/
COPY Engine.Persistence/Engine.Persistence.csproj Engine.Persistence/
COPY Engine.Runtime/Engine.Runtime.csproj Engine.Runtime/
COPY Engine.Activities/Engine.Activities.csproj Engine.Activities/
COPY Engine.Api/Engine.Api.csproj Engine.Api/

RUN dotnet restore Engine.Api/Engine.Api.csproj

COPY . .
RUN dotnet publish Engine.Api/Engine.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Engine.Api.dll"]
