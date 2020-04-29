FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS development
WORKDIR /app
COPY . .
RUN dotnet publish -c Release -o out
CMD ["dotnet", "watch", "run"]


FROM mcr.microsoft.com/dotnet/core/aspnet:3.1
WORKDIR /app
COPY --from=development /app/out .
EXPOSE 3306
ENTRYPOINT dotnet ClusterStarter.dll