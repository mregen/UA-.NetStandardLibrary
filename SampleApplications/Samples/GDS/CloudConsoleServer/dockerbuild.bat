dotnet build CloudGlobalDiscoveryServer.csproj
dotnet publish CloudGlobalDiscoveryServer.csproj -o ./publish
docker build -t cloudgds .
