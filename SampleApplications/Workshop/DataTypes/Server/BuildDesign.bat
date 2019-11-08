@echo off
setlocal

SET PATH=%PATH%;..\..\..\Scripts;..\..\Bin;..\..\..\Bin

echo Building ADI
Opc.Ua.ModelCompiler.exe -d2 ".\ADI\OpcUaAdiModel.xml" -cg ".\ADI\OpcUaAdiModel.csv" -o2 ".\ADI"

echo Building DI
Opc.Ua.ModelCompiler.exe -d2 ".\DI\OpcUaDiModel.xml" -cg ".\DI\OpcUaDiModel.csv" -o2 ".\DI"

echo Building MDIS
Opc.Ua.ModelCompiler.exe -d2 ".\MDIS\MDIS.xml" -cg ".\MDIS\MDIS.csv" -o2 ".\MDIS"

echo Building MTConnect
Opc.Ua.ModelCompiler.exe -d2 ".\MTConnect\MTConnectModel.xml" -cg ".\MTConnect\MTConnectModel.csv" -o2 ".\MTConnect"

echo Building PLCOpen
Opc.Ua.ModelCompiler.exe -d2 ".\PLCOpen\OpcUaPLCopenModel.xml" -cg ".\PLCOpen\OpcUaPLCopenModel.csv" -o2 ".\PLCOpen"

echo Building Robotics
Opc.Ua.ModelCompiler.exe -d2 ".\Robotics\OpcUaRoboticsModel.xml" -cg ".\Robotics\OpcUaRoboticsModel.csv" -o2 ".\Robotics"

echo Building Sercos
Opc.Ua.ModelCompiler.exe -d2 ".\Sercos\SercosModel.xml" -cg ".\Sercos\SercosModel.csv" -o2 ".\Sercos"

echo Building Server Internal ModelDesign2
Opc.Ua.ModelCompiler.exe -d2 ".\Instances\ModelDesign2.xml" -cg ".\Instances\ModelDesign2.csv" -o2 ".\Instances"

echo Success!




