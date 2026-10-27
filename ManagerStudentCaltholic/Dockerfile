# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["ManagerStudentCaltholic.csproj", "./"]
RUN dotnet restore "./ManagerStudentCaltholic.csproj"
COPY . .
RUN dotnet publish "ManagerStudentCaltholic.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Thiết lập múi giờ Việt Nam
ENV TZ=Asia/Ho_Chi_Minh
RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone

# Tạo sẵn thư mục lưu Log và Upload Giáo án, sau đó gán quyền sở hữu cho user phi đặc quyền ($APP_UID)
RUN mkdir -p /app/logs /app/wwwroot/uploads/lesson_plans \
    && chown -R $APP_UID:$APP_UID /app

COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .

# SEC-11 FIX: Chuyển sang chạy bằng tài khoản non-root có sẵn của .NET 8.0
USER $APP_UID

EXPOSE 5000

# SEC-11 FIX: Gọi trực tiếp file DLL cố định, không khởi chạy qua sh -c và ls/grep
ENTRYPOINT ["dotnet", "ManagerStudentCaltholic.dll"]