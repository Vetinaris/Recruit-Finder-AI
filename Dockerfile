FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["Recruit_Finder_AI.csproj", "./"]
RUN dotnet restore "Recruit_Finder_AI.csproj"
COPY . .
RUN dotnet publish "Recruit_Finder_AI.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Recruit_Finder_AI.dll"]