FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/EntryService/EntryService.csproj", "EntryService/"]
RUN dotnet restore "EntryService/EntryService.csproj"

COPY src/EntryService/ EntryService/
RUN dotnet publish "EntryService/EntryService.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

<<<<<<< HEAD
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

=======
>>>>>>> 76cf6c5d4e4f8af03b387c6fe57874ffec4b56d2
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "EntryService.dll"]
