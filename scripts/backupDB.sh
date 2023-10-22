#!/bin/bash
DATE=$(date +%Y-%m-%d_%H%M%S)
BACKUP_DIR="/home/app/backup"
BACKUP_FILE="$BACKUP_DIR/full_backup_$DATE.sql"

# Создать папку backup, если она не существует
sudo mkdir -p $BACKUP_DIR

# Создать новый бэкап
docker exec gptipsbot-bd pg_dumpall -c -U postgres > $BACKUP_FILE

# Проверить количество бэкапов в папке
NUM_BACKUPS=$(ls -t $BACKUP_DIR | grep "full_backup" | wc -l)

# Если количество бэкапов больше 5, удалить самый старый
if [ $NUM_BACKUPS -gt 5 ]; then
    OLDEST_BACKUP=$(ls -t $BACKUP_DIR | grep "full_backup" | tail -1)
    rm "$BACKUP_DIR/$OLDEST_BACKUP"
fi