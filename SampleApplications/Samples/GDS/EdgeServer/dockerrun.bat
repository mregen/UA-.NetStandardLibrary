rem start docker with mapped logs
docker run -it -p 58850-58852:58850-58852 -e 58850-58852 -h edgegds -v "/c/GDS:/root/.local/share/Microsoft/GDS" edgegds:latest
