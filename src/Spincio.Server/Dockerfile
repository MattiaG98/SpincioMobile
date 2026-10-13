# The online server as a container (ADR 0016), built from the repository root:
#   docker build -f src/Spincio.Server/Dockerfile -t spincio-server .
#   docker run -p 8080:8080 spincio-server
# Render (render.yaml) builds it this way and passes the port to listen on in PORT.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Spincio.Engine/Spincio.Engine.csproj src/Spincio.Engine/
COPY src/Spincio.Bots/Spincio.Bots.csproj src/Spincio.Bots/
COPY src/Spincio.Contracts/Spincio.Contracts.csproj src/Spincio.Contracts/
COPY src/Spincio.Server/Spincio.Server.csproj src/Spincio.Server/
RUN dotnet restore src/Spincio.Server/Spincio.Server.csproj
COPY src/Spincio.Engine/ src/Spincio.Engine/
COPY src/Spincio.Bots/ src/Spincio.Bots/
COPY src/Spincio.Contracts/ src/Spincio.Contracts/
COPY src/Spincio.Server/ src/Spincio.Server/
RUN dotnet publish src/Spincio.Server/Spincio.Server.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# Free hosting plans have little memory and a fraction of a CPU: the workstation GC fits them better.
ENV DOTNET_gcServer=0
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "exec dotnet Spincio.Server.dll --urls http://0.0.0.0:${PORT:-8080}"]
