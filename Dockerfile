FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY *.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /app/data
EXPOSE 8080
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV ConnectionStrings__DefaultConnection="Data Source=/app/data/bank.db"
ENTRYPOINT ["dotnet", "BankSupportDemo.dll"]
