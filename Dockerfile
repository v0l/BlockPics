# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /app

# Copy everything else and build
COPY . .
RUN rm -rf appsettings*.json
RUN dotnet publish -c Release -o out BlockPics/BlockPics.csproj

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:6.0

#install ffmpeg
RUN apt update && apt install -y --no-install-recommends ffmpeg

WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "BlockPics.dll"]