#!/bin/bash
# ==============================================================================
# SCRIPT PHỤC HỒI DỮ LIỆU TỪ BẢN SAO LƯU (TASK-712)
# Sử dụng: ./restore.sh /path/to/backup_file.sql.gz
# ==============================================================================

BACKUP_FILE=$1
CONTAINER_NAME="parish_db"
DB_NAME="parish_management"
DB_USER="postgres"

if [ -z "$BACKUP_FILE" ]; then
    echo "Lỗi: Vui lòng chỉ định đường dẫn file backup cần khôi phục!"
    echo "Cú pháp: ./restore.sh /var/backups/parish_db/backup_parish_management_xxxx.sql.gz"
    exit 1
fi

if [ ! -f "$BACKUP_FILE" ]; then
    echo "Lỗi: Không tìm thấy file $BACKUP_FILE!"
    exit 1
fi

echo "=================================================================="
echo "CẢNH BÁO: Thao tác này sẽ ghi đè toàn bộ dữ liệu trong database: $DB_NAME"
echo "File khôi phục: $BACKUP_FILE"
echo "=================================================================="
read -p "Bạn có chắc chắn muốn khôi phục không? (gõ 'YES' để xác nhận): " CONFIRM

if [ "$CONFIRM" != "YES" ]; then
    echo "Hủy bỏ tiến trình khôi phục."
    exit 0
fi

echo "[$(date)] Đang ngắt toàn bộ kết nối active đến $DB_NAME..."
docker exec -t $CONTAINER_NAME psql -U $DB_USER -c "
SELECT pg_terminate_backend(pid) 
FROM pg_stat_activity 
WHERE datname = '$DB_NAME' AND pid <> pg_backend_pid();"

echo "[$(date)] Đang nạp dữ liệu từ file backup vào database..."
gunzip -c "$BACKUP_FILE" | docker exec -i $CONTAINER_NAME psql -U $DB_USER -d $DB_NAME

if [ $? -eq 0 ]; then
    echo "[$(date)] PHỤC HỒI DỮ LIỆU THÀNH CÔNG!"
else
    echo "[$(date)] LỖI: Tiến trình phục hồi gặp lỗi!" >&2
    exit 1
fi