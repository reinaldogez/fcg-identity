FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY ["fcg-identity.slnx", "./"]
COPY ["nuget.config", "./"]
COPY ["src/Fcg.Identity.Domain/Fcg.Identity.Domain.csproj",                 "src/Fcg.Identity.Domain/"]
COPY ["src/Fcg.Identity.Application/Fcg.Identity.Application.csproj",        "src/Fcg.Identity.Application/"]
COPY ["src/Fcg.Identity.Infrastructure/Fcg.Identity.Infrastructure.csproj", "src/Fcg.Identity.Infrastructure/"]
COPY ["src/Fcg.Identity.Api/Fcg.Identity.Api.csproj",                       "src/Fcg.Identity.Api/"]
# Feed NuGet do GitHub Packages exige token mesmo para package público. O token entra via
# BuildKit secret mount e vive só na layer efêmera — nunca ARG/ENV (vazaria na imagem). Montar
# o secret não basta: é preciso autenticar o source antes do restore (--store-password-in-clear-text
# é exigência do provider NuGet do GitHub em Linux).
RUN --mount=type=secret,id=gh_token \
    dotnet nuget update source github-fcg \
      --username x --password "$(cat /run/secrets/gh_token)" --store-password-in-clear-text \
      --configfile nuget.config \
 && dotnet restore "src/Fcg.Identity.Api/Fcg.Identity.Api.csproj"
COPY src/ src/
RUN dotnet publish "src/Fcg.Identity.Api/Fcg.Identity.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
# A imagem alpine não traz ICU e roda em globalization-invariant mode, que o
# Microsoft.Data.SqlClient recusa. Instala o ICU e desliga o modo invariante.
RUN apk add --no-cache icu-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Fcg.Identity.Api.dll"]
