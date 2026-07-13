# Build from repo root: docker build -t lgbapp-api .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY LGBApp.Backend/LGBApp.Backend.csproj LGBApp.Backend/
RUN dotnet restore LGBApp.Backend/LGBApp.Backend.csproj
COPY LGBApp.Backend/ LGBApp.Backend/
RUN dotnet publish LGBApp.Backend/LGBApp.Backend.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /data /app/uploads

ENV ASPNETCORE_ENVIRONMENT=Production
ENV Database__Provider=Sqlite
ENV ConnectionStrings__DefaultConnection=Data Source=/data/lgbapp.db
ENV DISABLE_HTTPS_REDIRECTION=true
ENV AllowedHosts=*
ENV SEED_FULL=false

EXPOSE 8080
# Do not use Docker VOLUME — Railway requires its own volume mount at /data
# Railway injects PORT — bind to that (fallback 8080 for local docker runs)
ENTRYPOINT ["sh", "-c", "mkdir -p /data /app/uploads && echo Starting on PORT=${PORT:-8080} && dotnet LGBApp.Backend.dll --urls http://0.0.0.0:${PORT:-8080}"]
