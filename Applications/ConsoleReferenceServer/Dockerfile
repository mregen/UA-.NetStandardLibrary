#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

#Depending on the operating system of the host machines(s) that will build or run the containers, the image specified in the FROM statement may need to be changed.
#For more information, please see https://aka.ms/containercompat

FROM mcr.microsoft.com/dotnet/runtime:3.1 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:3.1 AS build
WORKDIR /src
COPY ["Applications/ConsoleReferenceServer/ConsoleReferenceServer.csproj", "Applications/ConsoleReferenceServer/"]
COPY ["Stack/Opc.Ua.Bindings.Https/Opc.Ua.Bindings.Https.csproj", "Stack/Opc.Ua.Bindings.Https/"]
COPY ["Stack/Opc.Ua.Core/Opc.Ua.Core.csproj", "Stack/Opc.Ua.Core/"]
COPY ["Libraries/Opc.Ua.Security.Certificates/Opc.Ua.Security.Certificates.csproj", "Libraries/Opc.Ua.Security.Certificates/"]
COPY ["Libraries/Opc.Ua.Configuration/Opc.Ua.Configuration.csproj", "Libraries/Opc.Ua.Configuration/"]
COPY ["Libraries/Opc.Ua.Server/Opc.Ua.Server.csproj", "Libraries/Opc.Ua.Server/"]
COPY ["Applications/Quickstarts.Servers/Quickstarts.Servers.csproj", "Applications/Quickstarts.Servers/"]
RUN dotnet restore "Applications/ConsoleReferenceServer/ConsoleReferenceServer.csproj"
COPY . .
WORKDIR "/src/Applications/ConsoleReferenceServer"
RUN dotnet build "ConsoleReferenceServer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ConsoleReferenceServer.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ConsoleReferenceServer.dll"]