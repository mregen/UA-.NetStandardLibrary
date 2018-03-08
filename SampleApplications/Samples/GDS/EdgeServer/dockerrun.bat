rem start docker with mapped logs
docker run -it -p 58820:58822 -e 58820 -h edgegds -v "/c/GDS:/root/.local/share/Microsoft/GDS/Logs" edgegds:latest
