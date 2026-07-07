#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
TEMPLATE="$SCRIPT_DIR/mihomo/config.yaml.template"
SERVICE_FILE="$SCRIPT_DIR/mihomo/mihomo.service"
MIHOMO_BIN="/usr/local/bin/mihomo"
MIHOMO_DIR="/etc/mihomo"

usage() {
    cat <<'EOF'
Устанавливает mihomo на Ubuntu-хост и поднимает HTTP-прокси :10809 для GPTipsBot.

Использование:
  sudo ./scripts/setup-mihomo.sh [URL_ПОДПИСКИ]

Если URL не передан, скрипт попробует взять HAPP_SUBSCRIPTION_URL из .env в корне репозитория.

После установки:
  1. Убедитесь, что в .env бота указано:
       HAPP_PROXY_IP=host.docker.internal
       HAPP_PROXY_PORT=10809
  2. Перезапустите бота: docker compose up -d --build
  3. Проверьте: curl -x http://127.0.0.1:10809 https://api.openai.com/v1/models -H "Authorization: Bearer $OPENAI_TOKEN"
EOF
}

if [[ "${1:-}" == "-h" || "${1:-}" == "--help" ]]; then
    usage
    exit 0
fi

if [[ "$EUID" -ne 0 ]]; then
    echo "Запустите скрипт с sudo: sudo $0 [URL_ПОДПИСКИ]"
    exit 1
fi

SUBSCRIPTION_URL="${1:-}"

if [[ -z "$SUBSCRIPTION_URL" && -f "$REPO_ROOT/.env" ]]; then
    SUBSCRIPTION_URL="$(grep -E '^HAPP_SUBSCRIPTION_URL=' "$REPO_ROOT/.env" | cut -d= -f2- | tr -d '"' | tr -d "'")"
fi

if [[ -z "$SUBSCRIPTION_URL" ]]; then
    echo "Ошибка: укажите URL подписки аргументом или добавьте HAPP_SUBSCRIPTION_URL в .env"
    usage
    exit 1
fi

ARCH="$(uname -m)"
case "$ARCH" in
    x86_64) MIHOMO_ARCH="amd64" ;;
    aarch64) MIHOMO_ARCH="arm64" ;;
    *)
        echo "Неподдерживаемая архитектура: $ARCH"
        exit 1
        ;;
esac

echo "==> Проверка подписки..."
if ! curl -fsSL -A "Happ/1.0" --max-time 20 "$SUBSCRIPTION_URL" | head -c 50 | grep -qE '^(vless|vmess|trojan|ss|hysteria2|hy2)://'; then
    echo "Предупреждение: ответ подписки не похож на список proxy-ссылок. Продолжаем установку."
fi

echo "==> Установка mihomo..."
VERSION="$(curl -fsSL https://api.github.com/repos/MetaCubeX/mihomo/releases/latest | grep '"tag_name"' | head -1 | cut -d'"' -f4)"
ARCHIVE="mihomo-linux-${MIHOMO_ARCH}-${VERSION}.gz"
TMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TMP_DIR"' EXIT

curl -fsSL -o "$TMP_DIR/$ARCHIVE" "https://github.com/MetaCubeX/mihomo/releases/download/${VERSION}/${ARCHIVE}"
gunzip -c "$TMP_DIR/$ARCHIVE" > "$TMP_DIR/mihomo"
chmod +x "$TMP_DIR/mihomo"
install -m 755 "$TMP_DIR/mihomo" "$MIHOMO_BIN"
"$MIHOMO_BIN" -v

echo "==> Конфигурация $MIHOMO_DIR..."
mkdir -p "$MIHOMO_DIR"
sed "s|__SUBSCRIPTION_URL__|$SUBSCRIPTION_URL|g" "$TEMPLATE" > "$MIHOMO_DIR/config.yaml"
chmod 600 "$MIHOMO_DIR/config.yaml"

echo "==> Установка systemd-сервиса..."
install -m 644 "$SERVICE_FILE" /etc/systemd/system/mihomo.service
systemctl daemon-reload
systemctl enable mihomo
systemctl restart mihomo

sleep 2

if systemctl is-active --quiet mihomo; then
    echo "==> mihomo запущен."
else
    echo "Ошибка: mihomo не стартовал. Логи:"
    journalctl -u mihomo -n 30 --no-pager
    exit 1
fi

if ss -tln | grep -q ':10809 '; then
    echo "==> HTTP-прокси слушает порт 10809."
else
    echo "Предупреждение: порт 10809 не найден. Проверьте: journalctl -u mihomo -f"
fi

cat <<EOF

Готово.

Проверка на хосте:
  curl -x http://127.0.0.1:10809 https://api.openai.com/v1/models -H "Authorization: Bearer \$OPENAI_TOKEN"

Проверка из контейнера бота:
  docker exec gptipsbot-app curl -x http://host.docker.internal:10809 https://api.openai.com/v1/models -H "Authorization: Bearer \$OPENAI_TOKEN"

Управление:
  sudo systemctl status mihomo
  sudo systemctl restart mihomo
  sudo journalctl -u mihomo -f
EOF
