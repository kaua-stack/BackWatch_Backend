FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/ApiReliabilitySuite.API/ApiReliabilitySuite.API.csproj", "src/ApiReliabilitySuite.API/"]
COPY ["src/ApiReliabilitySuite.Application/ApiReliabilitySuite.Application.csproj", "src/ApiReliabilitySuite.Application/"]
COPY ["src/ApiReliabilitySuite.Domain/ApiReliabilitySuite.Domain.csproj", "src/ApiReliabilitySuite.Domain/"]
COPY ["src/ApiReliabilitySuite.Infrastructure/ApiReliabilitySuite.Infrastructure.csproj", "src/ApiReliabilitySuite.Infrastructure/"]
COPY ["src/ApiReliabilitySuite.Workers/ApiReliabilitySuite.Workers.csproj", "src/ApiReliabilitySuite.Workers/"]

RUN dotnet restore "src/ApiReliabilitySuite.API/ApiReliabilitySuite.API.csproj"

COPY . .
WORKDIR "/src/src/ApiReliabilitySuite.API"
RUN dotnet publish "ApiReliabilitySuite.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
HEALTHCHECK --interval=10s --timeout=3s --retries=10 CMD curl --fail http://localhost:8080/healthz || exit 1
ENTRYPOINT ["dotnet", "ApiReliabilitySuite.API.dll"]
