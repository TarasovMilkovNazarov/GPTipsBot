# Деплой GPTipsBot (Yandex COI + Caddy)

Единый источник правды для контейнеров на `hackers-a1` (`51.250.9.203` / внутренний `10.128.0.34`) — **metadata-ключ `docker-compose`** у ВМ.  
Его читает `yc-container-daemon` и применяет через `docker compose -p coi`.

Публичный HTTPS — Caddy на edge `skolkokomu` (`111.88.254.14`).

| Что | Значение |
|---|---|
| ВМ (бот) | `hackers-a1` / id `fhmvraffnqmoq8e3cn7t` |
| Metadata key | `docker-compose` |
| Образ | `alanextar/gptipsbot:latest` |
| Контейнер | `gptipsbot-app` (имя от COI может иметь префикс) |
| Порт на бот-ВМ | `:80` → контейнер `:80` |
| Публичный URL | **https://gptips.skolkokomu.ru** |
| Postgres | `gptipsbot-bd`, хост `:5432` |
| Watchtower | авто-pull/restart `gptipsbot-app` |

**Web UI вшит в тот же Docker-образ** (`GPTipsBot/wwwroot` собирается в `Dockerfile`). Отдельный контейнер / static deploy для фронта **не нужен**.

---

## Caddy — нужно ли менять?

**Нет**, если блок уже есть (сейчас так и есть на `111.88.254.14`):

```caddy
# GPTipsBot — UI + API + YooKassa webhooks
gptips.skolkokomu.ru {
	encode zstd gzip
	reverse_proxy 10.128.0.34:80
}
```

Весь трафик (`/`, `/api/*`, `/webhooks/yookassa`, статика) идёт в один ASP.NET-хост.  
Менять Caddyfile нужно только если домена ещё нет или proxy указывает не на `:80` бот-ВМ.

После правки Caddy (если всё же меняли):

```bash
ssh alanextar@111.88.254.14
sudo caddy validate --config /etc/caddy/Caddyfile
sudo systemctl reload caddy
```

DNS: `gptips.skolkokomu.ru` → `111.88.254.14` (уже так).

---

## A. Обновить бот + веб (обычный релиз)

Когда меняется код бота или UI — достаточно нового образа.

### 1. Собрать и запушить

CI: push в ветку `release` → `.github/workflows/docker-publish.yml` пушит  
`alanextar/gptipsbot:<calver>` и `:latest` (в образ входит `npm run build` → `wwwroot`).

Вручную:

```bash
docker build -t alanextar/gptipsbot:latest .
docker push alanextar/gptipsbot:latest
```

### 2. Дождаться Watchtower или рестарт

```bash
ssh alanextar@51.250.9.203
docker pull alanextar/gptipsbot:latest
# имя контейнера смотри в docker ps | grep gptips
docker restart <gptipsbot-app-container>
```

Compose в metadata **не трогать**, если env/ports не менялись.

### 3. Проверить

```bash
curl -sI https://gptips.skolkokomu.ru/health
curl -sI https://gptips.skolkokomu.ru/
curl -s https://gptips.skolkokomu.ru/api/config/public
```

Ожидание: `/health` → `ok`, `/` → HTML UI, `/api/config/public` → JSON с `botUsername`.

---

## B. Env для фронтенда (COI metadata)

Добавить/проверить в environment сервиса `gptipsbot` в COI `docker-compose`  
(скачать metadata → править → `yc compute instance add-metadata ...`).

| Переменная | Нужна? | Значение / зачем |
|---|---|---|
| `TELEGRAM_BOT_USERNAME` | **Да** (Login Widget) | `GPTipsBot` (без `@`). Имя бота для Telegram Login на сайте |
| `TELEGRAM_TOKEN` | Да (уже есть) | Токен бота; также для проверки подписи Login Widget |
| `ASPNETCORE_ENVIRONMENT` | Да | `Production` |
| `ASPNETCORE_URLS` | Обычно уже | `http://+:80` |
| `PG_CONNECTION_STRING` | Да (уже есть) | Shared Postgres |
| `OPENAI_TOKEN` / `HAPP_PROXY_*` | Да (уже есть) | Как для Telegram-бота |
| `YOOKASSA_*` | Если карта | Webhook уже на `https://gptips.skolkokomu.ru/webhooks/yookassa` |
| `YOOKASSA_RETURN_URL` | Опционально | Можно `https://gptips.skolkokomu.ru/` вместо t.me |

Пример фрагмента (секреты не коммитить):

```yaml
  gptipsbot:
    image: alanextar/gptipsbot:latest
    # container_name задаёт COI
    restart: always
    extra_hosts:
      - "host.docker.internal:host-gateway"
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:80
      TELEGRAM_BOT_USERNAME: GPTipsBot
      # TELEGRAM_TOKEN, PG_CONNECTION_STRING, OPENAI_TOKEN, HAPP_*, YOOKASSA_* — как сейчас
    ports:
      - "80:80"
```

Загрузка metadata:

```powershell
yc compute instance add-metadata fhmvraffnqmoq8e3cn7t --metadata-from-file docker-compose=coi-docker-compose.yml
```

---

## C. Telegram Login Widget (один раз)

1. `@BotFather` → бот → **Domain** / Login Widget domain → `gptips.skolkokomu.ru`
2. На сайте виджет подтянет `botUsername` из `/api/config/public` (env `TELEGRAM_BOT_USERNAME` или `AppConfig.BotName` после старта polling).

Без домена в BotFather кнопка Login не заработает (гость при этом работает).

---

## D. Forwarded headers / cookies

В приложении уже включены `X-Forwarded-For` / `X-Forwarded-Proto`.  
Cookie сессии: `SameSite=Lax`, `Secure=SameAsRequest` — за HTTPS Caddy всё ок.

Отдельный CORS для продакшена не нужен: UI и `/api` на одном origin `https://gptips.skolkokomu.ru`.

---

## E. Чеклист релиза веба

- [ ] Merge / push `release` → образ с актуальным `wwwroot`
- [ ] Watchtower обновил контейнер (или ручной `pull` + `restart`)
- [ ] В COI env есть `TELEGRAM_BOT_USERNAME=GPTipsBot`
- [ ] BotFather: domain `gptips.skolkokomu.ru`
- [ ] Caddy-блок `gptips.skolkokomu.ru` без изменений (если уже proxy на `10.128.0.34:80`)
- [ ] `https://gptips.skolkokomu.ru/` открывает чат
- [ ] `https://gptips.skolkokomu.ru/webhooks/yookassa` по-прежнему доступен

---

## F. Что нельзя делать

| Нельзя | Почему |
|---|---|
| Деплоить фронт отдельно на `/var/www/...` как TripWeave | UI уже в образе бота |
| Второй compose из `/opt/gptips...` рядом с COI | конфликт имён / recreate-луп |
| Коммитить полный COI compose с секретами | токены в git |
| Вешать веб на другой порт без правки Caddy | DNS/Caddy сейчас → `:80` |

---

## G. Полезные команды

```powershell
yc compute instance get fhmvraffnqmoq8e3cn7t --full
yc compute instance add-metadata fhmvraffnqmoq8e3cn7t --metadata-from-file docker-compose=coi-docker-compose.yml
```

```bash
# бот-ВМ
ssh alanextar@51.250.9.203
docker ps | grep gptips
docker logs --tail 80 <gptips-container>

# edge / Caddy
ssh alanextar@111.88.254.14
sudo cat /etc/caddy/Caddyfile
sudo systemctl status caddy
```
