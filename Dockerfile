# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /app

#install ffmpeg
RUN apt-get install -y ffmpeg

# Copy everything else and build
COPY . .
RUN rm -rf appsettings*.json
RUN dotnet publish -c Release -o out BlockPics/BlockPics.csproj

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:6.0
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "BlockPics.dll"]