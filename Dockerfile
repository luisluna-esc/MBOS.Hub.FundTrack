FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY MBOS.Hub.FundTrack.sln ./
COPY FundTrack.Web/FundTrack.Web.csproj FundTrack.Web/
COPY FundTrack.Application/FundTrack.Application.csproj FundTrack.Application/
COPY FundTrack.core/FundTrack.core.csproj FundTrack.core/
COPY FundTrack.Infrastructure/FundTrack.Infrastructure.csproj FundTrack.Infrastructure/
RUN dotnet restore FundTrack.Web/FundTrack.Web.csproj

COPY FundTrack.Web/ FundTrack.Web/
COPY FundTrack.Application/ FundTrack.Application/
COPY FundTrack.core/ FundTrack.core/
COPY FundTrack.Infrastructure/ FundTrack.Infrastructure/

RUN dotnet publish FundTrack.Web/FundTrack.Web.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

ENV ASPNETCORE_URLS=http://+:8080
# Los adjuntos de los arqueos van en un volumen montado aquí, para que no se pierdan al actualizar.
ENV Attachments__RootPath=/app/App_Data/attachments
EXPOSE 8080

ENTRYPOINT ["dotnet", "FundTrack.Web.dll"]
