rem docker run -it -p 58800:58800 -e 58800 -h dockergds gds:latest
docker run -it -p 58810:58810 -e 58810 -h dockergds -v "/c/GDS:/root/.local/share/OPC Foundation/GDS/Logs" gds:latest
