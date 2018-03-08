FROM microsoft/dotnet:runtime:latest

COPY ./publish /publish
WORKDIR /publish

ENTRYPOINT ["dotnet", "EdgeGlobalDiscoveryServer.dll"]
