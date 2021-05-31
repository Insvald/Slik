ARG FRAMEWORK
FROM mcr.microsoft.com/dotnet/aspnet:${FRAMEWORK} AS base
EXPOSE 3100

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
ARG FRAMEWORK
WORKDIR /sln
COPY . .
RUN dotnet publish "/sln/src/SlikCord/SlikCord.csproj" -c Release -o /app/publish --framework net${FRAMEWORK}

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SlikCord.dll"]