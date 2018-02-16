FROM microsoft/dotnet:2.0-sdk

COPY ./publish /publish
WORKDIR /publish

ENTRYPOINT ["dotnet", "CloudGlobalDiscoveryServer.dll"]
