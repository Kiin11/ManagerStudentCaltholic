FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy file .csproj 
COPY *.csproj ./
RUN dotnet restore

# Copy code and build Release
COPY . ./
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Setting TimeZone cho container
ENV TZ=Asia/Ho_Chi_Minh
RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone

# Copy 
COPY --from=build /app/publish .

EXPOSE 5000

ENTRYPOINT ["sh", "-c", "exec dotnet $(ls *.dll | grep -v 'System\\.' | grep -v 'Microsoft\\.' | head -n 1)"]