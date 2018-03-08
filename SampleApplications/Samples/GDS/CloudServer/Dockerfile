FROM microsoft/dotnet:runtime:latest

COPY ./publish /publish
WORKDIR /publish

ENTRYPOINT ["dotnet", "CloudGlobalDiscoveryServer.dll"]
