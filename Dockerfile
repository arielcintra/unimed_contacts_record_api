FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the dependency layer is cached between code changes.
COPY src/ContactsRecord.Domain/ContactsRecord.Domain.csproj src/ContactsRecord.Domain/
COPY src/ContactsRecord.Application/ContactsRecord.Application.csproj src/ContactsRecord.Application/
COPY src/ContactsRecord.Infrastructure/ContactsRecord.Infrastructure.csproj src/ContactsRecord.Infrastructure/
COPY src/ContactsRecord.Api/ContactsRecord.Api.csproj src/ContactsRecord.Api/
RUN dotnet restore src/ContactsRecord.Api/ContactsRecord.Api.csproj

COPY src/ src/
RUN dotnet publish src/ContactsRecord.Api/ContactsRecord.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "ContactsRecord.Api.dll"]
