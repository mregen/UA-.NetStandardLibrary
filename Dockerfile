FROM microsoft/dotnet:2.0.3-sdk

COPY . /build
#COPY /SampleApplications /build/SampleApplications
#COPY /Stack /build/Stack

WORKDIR /build

RUN dotnet restore "UA Core Library.sln"
RUN dotnet publish SampleApplications/Samples/GDS/ConsoleServer/NetCoreGlobalDiscoveryServer.csproj -o ../../../../out
COPY SampleApplications/Samples/GDS/ConsoleServer/Opc.Ua.GlobalDiscoveryServer.Config.xml out
WORKDIR /build/out

ENTRYPOINT ["dotnet", "NetCoreGlobalDiscoveryServer.dll"]
