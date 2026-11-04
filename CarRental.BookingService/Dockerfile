# syntax=docker/dockerfile:1
# Multi-stage build for CarRental.BookingService.
# Build context = repo-roden (docker build -f CarRental.BookingService/Dockerfile .),
# fordi servicen skal bruge CarRental.Shared + nuget.config fra roden.

# ---------- Stage 1: build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 1) Byg CarRental.Shared som NuGet-pakke -> /src/local-nuget-feed
COPY nuget.config ./
COPY CarRental.Shared/ CarRental.Shared/
RUN dotnet build CarRental.Shared/CarRental.Shared.csproj -c Release

# 2) Restore servicen. Kun .csproj kopieres først, så dette lag caches
#    så længe dependencies ikke ændres.
COPY CarRental.BookingService/CarRental.BookingService.csproj CarRental.BookingService/
RUN dotnet restore CarRental.BookingService/CarRental.BookingService.csproj

# 3) Kopiér resten af koden og publish
COPY CarRental.BookingService/ CarRental.BookingService/
RUN dotnet publish CarRental.BookingService/CarRental.BookingService.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- Stage 2: runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Lyt på HTTP 8080 inde i containeren (HTTPS håndteres ikke i dev-miljøet)
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Kør som non-root (brugeren 'app' findes i Microsofts .NET 8 images)
USER $APP_UID

ENTRYPOINT ["dotnet", "CarRental.BookingService.dll"]
