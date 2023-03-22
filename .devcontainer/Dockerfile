# See here for image contents: https://github.com/microsoft/vscode-dev-containers/tree/v0.177.0/containers/ubuntu/.devcontainer/base.Dockerfile

ARG VARIANT="6.0"
FROM mcr.microsoft.com/vscode/devcontainers/dotnet:${VARIANT}

RUN /usr/bin/pwsh -c "install-module az, Microsoft.Graph -Force -Scope AllUsers"

# Install Azure functions core tools
# This will add the func command for the cli
RUN curl https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > microsoft.gpg \
    && sudo mv microsoft.gpg /etc/apt/trusted.gpg.d/microsoft.gpg

RUN sudo sh -c 'echo "deb [arch=amd64] https://packages.microsoft.com/repos/microsoft-ubuntu-$(lsb_release -cs)-prod $(lsb_release -cs) main" > /etc/apt/sources.list.d/dotnetdev.list' \
    && sudo apt-get update

RUN apt-get install azure-functions-core-tools-4

# Install bicep
# Fetch the latest Bicep CLI binary
RUN curl -Lo bicep https://github.com/Azure/bicep/releases/latest/download/bicep-linux-x64
# Mark it as executable
RUN chmod +x ./bicep
# Add bicep to your PATH (requires admin)
RUN sudo mv ./bicep /usr/local/bin/bicep
