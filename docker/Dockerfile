FROM mcr.microsoft.com/dotnet/runtime:6.0

ARG PBI_TOOLS_VERSION

WORKDIR /app

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

RUN apt-get update && apt-get upgrade --no-install-recommends
RUN apt-get install --no-install-recommends wget unzip git git-lfs -y

RUN wget -O pbi-tools.zip "https://github.com/pbi-tools/pbi-tools/releases/download/${PBI_TOOLS_VERSION}/pbi-tools.core.${PBI_TOOLS_VERSION}_linux-x64.zip"
RUN unzip pbi-tools.zip -d pbi-tools

RUN chmod +x /app/pbi-tools/pbi-tools.core

CMD ["/app/pbi-tools/pbi-tools.core", "info"]