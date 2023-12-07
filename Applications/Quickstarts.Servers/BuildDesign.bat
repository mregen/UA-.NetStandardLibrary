@echo off
setlocal

REM if docker is not available, ensure the Opc.Ua.ModelCompiler.exe is in the PATH
set MODELCOMPILER=D:\Source\Repos\UA-ModelCompiler\build\bin\Docker\net6.0\Opc.Ua.ModelCompiler.exe
REM The version of the ModelCompiler from the OPCF to use as docker container
REM 
REM set MODELCOMPILERIMAGE=ghcr.io/opcf-members/ua-modelcompiler:latest

set MODELCOMPILERIMAGE=ghcr.io/opcfoundation/ua-modelcompiler:2.3.0
REM private build until the official image is updated
set MODELCOMPILERIMAGE=ghcr.io/mregen/ua-modelcompiler:latest-docker-nodeset2
set MODELROOT=.

echo pull latest modelcompiler from github container registry
REM docker login ghcr.io -u <user> -p <pw>
echo login to ghcr
REM docker login ghcr.io -u mregen -p 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
IF ERRORLEVEL 1 goto nodocker

docker pull %MODELCOMPILERIMAGE%
SET ERRORLEVEL=20
IF ERRORLEVEL 1 (
:nodocker
    Echo The docker command to download ModelCompiler failed. Using local PATH instead to execute ModelCompiler.
) ELSE (
    Echo Successfully pulled the latest docker container for ModelCompiler.
    set MODELROOT=/model
    set MODELCOMPILER=docker run -v "%CD%:/model" -it --rm --name ua-modelcompiler %MODELCOMPILERIMAGE% 
)

echo Building TestData
%MODELCOMPILER% compile -version v104 -id 1000 -d2 "%MODELROOT%/TestData/TestDataDesign.xml" -cg "%MODELROOT%/TestData/TestDataDesign.csv" -o2 "%MODELROOT%/TestData"
IF %ERRORLEVEL% EQU 0 echo Success!

echo Building MemoryBuffer
%MODELCOMPILER% compile -version v104 -id 1000 -d2 "%MODELROOT%/MemoryBuffer/MemoryBufferDesign.xml" -cg "%MODELROOT%/MemoryBuffer/MemoryBufferDesign.csv" -o2 "%MODELROOT%/MemoryBuffer" 
IF %ERRORLEVEL% EQU 0 echo Success!

echo Building BoilerDesign
%MODELCOMPILER% compile -version v104 -id 1000 -d2 "%MODELROOT%/Boiler/BoilerDesign.xml" -cg "%MODELROOT%/Boiler/BoilerDesign.csv" -o2 "%MODELROOT%/Boiler"
IF %ERRORLEVEL% EQU 0 echo Success!

