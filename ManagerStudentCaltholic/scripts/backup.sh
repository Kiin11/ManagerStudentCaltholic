#!/bin/bash
# ==============================================================================
# SCRIPT TỰ ĐỘNG SAO LƯU POSTGRESQL & XOAY VÒNG 7 NGÀY (TASK-711)
# ==============================================================================

# Thông số kết nối
CONTAINER_NAME="parish_db"
DB_NAME="parish_management"
DB_USER="postgres"
BACKUP_DIR="/var/backups/parish_db"
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
FILENAME="backup_${DB_NAME}_${TIMESTAMP}.sql.gz"

# Tạo thư mục lưu backup nếu chưa có
mkdir -p "$BACKUP_DIR"

echo "[$(date)] Bắt đầu tiến trình sao lưu cơ sở dữ liệu: $DB_NAME..."

# 1. Gọi pg_dump từ trong container và nén gzip trực tiếp
docker exec -t $CONTAINER_NAME pg_dump -U $DB_USER -d $DB_NAME | gzip > "$BACKUP_DIR/$FILENAME"

if [ $? -eq 0 ]; then
    FILE_SIZE=$(du -h "$BACKUP_DIR/$FILENAME" | cut -f1)
    echo "[$(date)] Sao lưu hoàn tất thành công! File: $BACKUP_DIR/$FILENAME (Dung lượng: $FILE_SIZE)"
else
    echo "[$(date)] LỖI: Sao lưu thất bại!" >&2
    exit 1
fi

# 2. Xoay vòng bản sao lưu: Tự động xóa các file backup cũ hơn 7 ngày để tiết kiệm dung lượng đĩa
echo "[$(date)] Dọn dẹp các bản sao lưu cũ hơn 7 ngày..."
find "$BACKUP_DIR" -type f -name "backup_${DB_NAME}_*.sql.gz" -mtime +7 -exec rm -f {} \;

echo "[$(date)] Hoàn tất chu kỳ sao lưu và dọn dẹp."