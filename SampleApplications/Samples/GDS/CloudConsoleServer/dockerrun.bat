rem start docker with mapped logs
docker run -it -p 58820:58822 -e 58820 -h cloudgds -v "/c/GDS:/root/.local/share/OPC Foundation/GDS/Logs" cloudgds:latest
