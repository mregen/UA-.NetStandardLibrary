@echo off
setlocal

SET PATH=%PATH%;..\..\..\Scripts;..\..\Bin;..\..\..\Bin

echo Building ModelDesign1
Opc.Ua.ModelCompiler.exe -version v104 -d2 ".\Types\ModelDesign1.xml" -cg ".\Types\ModelDesign1.csv" -o2 ".\Types"
echo Success!

echo Building ADI
Opc.Ua.ModelCompiler.exe -version v104 -d2 ".\ADI\OpcUaAdiModel.xml" -cg ".\ADI\OpcUaAdiModel.csv" -o2 ".\ADI"

echo Building DI
Opc.Ua.ModelCompiler.exe -version v104 -d2 ".\DI\OpcUaDiModel.xml" -cg ".\DI\OpcUaDiModel.csv" -o2 ".\DI"

echo Building MDIS
Opc.Ua.ModelCompiler.exe -version v104 -d2 ".\MDIS\MDIS.xml" -cg ".\MDIS\MDIS.csv" -o2 ".\MDIS"

echo Building MTConnect
Opc.Ua.ModelCompiler.exe -version v104 -d2 ".\MTConnect\MTConnectModel.xml" -cg ".\MTConnect\MTConnectModel.csv" -o2 ".\MTConnect"

echo Building PLCOpen
Opc.Ua.ModelCompiler.exe -version v104 -d2 ".\PLCOpen\OpcUaPLCopenModel.xml" -cg ".\PLCOpen\OpcUaPLCopenModel.csv" -o2 ".\PLCOpen"

echo Building Robotics
Opc.Ua.ModelCompiler.exe -version v104 -d2 ".\Robotics\OpcUaRoboticsModel.xml" -cg ".\Robotics\OpcUaRoboticsModel.csv" -o2 ".\Robotics"

echo Building Sercos
Opc.Ua.ModelCompiler.exe -version v104 -d2 ".\Sercos\SercosModel.xml" -cg ".\Sercos\SercosModel.csv" -o2 ".\Sercos"

echo Building Fortiss DI
Opc.Ua.ModelCompiler.exe -version v104 -d2 ".\fortiss_di\fortissDiModel.xml" -cg ".\fortiss_di\fortissDiModel.csv" -o2 ".\fortiss_di"

echo Building Fortiss Robotics
Opc.Ua.ModelCompiler.exe -version v104 -d2 ".\fortiss_robotics\fortissRoboticsModel.xml" -cg ".\fortiss_robotics\fortissRoboticsModel.csv" -o2 ".\fortiss_robotics"

echo Building Kuka Iiwa
Opc.Ua.ModelCompiler.exe -version v104 -d2 ".\kuka_iiwa\kukaIiwaModel.xml" -cg ".\kuka_iiwa\kukaIiwaModel.csv" -o2 ".\kuka_iiwa"



