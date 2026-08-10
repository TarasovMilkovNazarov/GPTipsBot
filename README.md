# GPTipsBot

What is it?
Free telegram bot with ChatGPT integration and DALL-E without any subscriptions

## Web UI (bota.chat-style)

Browser chat lives in [`web/`](web/) and is served from `GPTipsBot/wwwroot`.

```bash
cd web && npm install && npm run build   # → GPTipsBot/wwwroot
# then run the ASP.NET host; open http://localhost:5000
```

Dev with hot reload: run the API on `:5000`, then `cd web && npm run dev` (Vite proxies `/api`).

Optional env: `TELEGRAM_BOT_USERNAME` for the Login Widget (defaults to `AppConfig.BotName` / `GPTipsBot`).

Prod: **https://gptips.skolkokomu.ru** — см. [deploy/README.md](deploy/README.md) (COI + Caddy, без отдельного фронт-деплоя).

MVP: guest cookie sessions, Telegram Login (shared wallet), streaming chat, models, image gen, OCR, voice STT, RU/EN.

* Board: https://trello.com/b/hyydi02P/chatgtp-telegram-bot
* Telegram bot:
  * PROD https://t.me/GPTipsBot
  * CLOUD https://t.me/GPTipBot
* Секреты: https://github.com/organizations/TarasovMilkovNazarov/settings/variables/actions
* Hosting: https://console.cloud.yandex.ru/folders/b1ghg7fp1esojrsq87tq
* Logs: https://console.cloud.yandex.ru/folders/b1ghg7fp1esojrsq87tq/logging/group/e23pildlggn1clcjtr5u/logs?from=now-1h&to=now&size=100&linesInRow=1
* Registry: https://hub.docker.com/r/alanextar/gptipsbot
* Auto Deploy: пушим ветку release

## Групповые чаты

В группах и супергруппах доступно только:

* `/ask вопрос` — вопрос к ИИ (ответ виден всем, списание с автора);
* `/image` — генерация картинок (видны всем, баланс списывается с автора);
* `/summary` — краткое содержание за день (в чат). **3 бесплатных** использования навсегда, дальше **0.3⭐** (~3 обычных GPT-запроса). Бесплатные summary дневным лимитом не обновляются.
* упоминание бота или reply на его сообщение — тоже вопрос к ИИ.

Остальные команды в группах недоступны — ими пользуйтесь в личке с ботом.

Реклама в группах не отправляется.

В меню группы Telegram показывает только `/ask`, `/image` и `/summary`. Полный список команд — в личке.

## Inline-режим

В любом чате можно набрать `@GPTipsBot` (или `@GPTipBot` на cloud):

* `ask вопрос` — ответ ChatGPT в этот чат;
* `image описание` — генерация картинки по промпту.

Примеры: `@GPTipsBot ask Что такое нейтрино?`, `@GPTipsBot image кот в космосе`.

В BotFather нужно включить:

1. `/setinline` — Inline Mode;
2. `/setinlinefeedback` — Enabled (иначе бот не получит `chosen_inline_result` и не подставит ответ/картинку).

При **включённом privacy mode** Telegram присылает боту только команды, упоминания и reply. Чтобы бот видел все сообщения группы для `/summary`, выключите Group Privacy: `@BotFather` → `/setprivacy` → Disable.

## VPN / OpenAI (mihomo)

Проверка прямого доступа к OpenAI через VPN-подписку Happ. Работает **только по админской команде** `/vpn_check` — фоновой джобы нет.

### Схема

```
Подписка Happ (URL)
        ↓
mihomo на VPS-хосте (systemd)  →  VPN-серверы из подписки
        ↓
HTTP-прокси :10809 на хосте
        ↓
gptipsbot-app (Docker)  →  api.openai.com
```

| Компонент | Где живёт | Роль |
|-----------|-----------|------|
| **mihomo** | VPS-хост, `systemd` | Качает подписку, поднимает HTTP-прокси |
| **gptipsbot** | Docker-контейнер | Ходит в OpenAI через прокси хоста |
| **Watchtower / yc-container-daemon** | Docker | Обновляет только образ бота — mihomo не трогает |

Основной чат и DALL-E идут напрямую в `api.openai.com` через HTTP-прокси mihomo. Токен — `OPENAI_TOKEN` из окружения.

### Что делает `/vpn_check`

1. **Subscription** — доступен ли URL подписки (если задан `HAPP_SUBSCRIPTION_URL`).
2. **OpenAI** — отвечает ли `api.openai.com` через HTTP-прокси mihomo.

Пример ответа:

```
#vpn_openai_check
Subscription: OK (1234 bytes)
Proxy: host.docker.internal:10809
OpenAI: OK
Duration: 842 ms
```

### Установка mihomo на VPS (один раз)

```bash
# В .env укажите HAPP_SUBSCRIPTION_URL, затем на VPS:
chmod +x scripts/setup-mihomo.sh
sudo ./scripts/setup-mihomo.sh
```

Скрипт:
- скачивает [mihomo](https://github.com/MetaCubeX/mihomo) в `/usr/local/bin/mihomo`;
- создаёт `/etc/mihomo/config.yaml` из подписки;
- регистрирует `systemd`-сервис на порту **10809**.

Проверка на хосте:

```bash
sudo systemctl status mihomo
curl -x http://127.0.0.1:10809 https://api.openai.com/v1/models \
  -H "Authorization: Bearer $OPENAI_TOKEN"
```

Ответ `401` без ключа — нормально: прокси работает, нужен валидный `OPENAI_TOKEN`.

### Переменные бота

В `.env` / конфиге деплоя контейнера:

```env
HAPP_SUBSCRIPTION_URL=https://bot.tiroel.ru/t-consult_service/xxxxxxx/486xxxxxx
HAPP_PROXY_IP=host.docker.internal
HAPP_PROXY_PORT=10809
OPENAI_TOKEN=sk-...
```

`host.docker.internal` — адрес VPS-хоста изнутри контейнера. В `docker-compose.yml` для этого добавлен `extra_hosts`:

```yaml
extra_hosts:
  - "host.docker.internal:host-gateway"
```

На проде (yc-container-daemon) те же переменные и `extra_hosts` нужно прописать в конфиге контейнера `gptipsbot-app`.

### Проверка из контейнера

```bash
docker exec gptipsbot-app curl -x http://host.docker.internal:10809 \
  https://api.openai.com/v1/models -H "Authorization: Bearer $OPENAI_TOKEN"
```

В Telegram: `/vpn_check` с админского аккаунта.

### Управление mihomo

```bash
sudo systemctl status mihomo
sudo systemctl restart mihomo
sudo journalctl -u mihomo -f
ss -tln | grep 10809
```

При смене подписки — отредактируйте `url` в `/etc/mihomo/config.yaml` и выполните `sudo systemctl restart mihomo`, либо перезапустите `setup-mihomo.sh`.

Конфиг и бинарник на хосте:

| Путь | Описание |
|------|----------|
| `/usr/local/bin/mihomo` | Бинарник |
| `/etc/mihomo/config.yaml` | Конфиг (подписка, порт 10809) |
| `/etc/systemd/system/mihomo.service` | Автозапуск |

## Как это работает
* На ВМ крутятся контейнеры: бот, БД, fluentbit. **mihomo** — отдельно на хосте, вне Docker.
* Watchtower обновляет образ `alanextar/gptipsbot:latest` и перезапускает контейнер бота. Mihomo при этом не перезапускается.
* Настроен crontab на создание бэкапов БД. Посмотреть настроенные джобы `sudo crontab -l`. Там будет видно какой скрипт он запускает
* Для рестора надо выполнить эту команду, **указав имя нужного файла**
```bash
cat /home/app/backup/full_backup_$DATE.sql | docker exec -i gptipsbot-bd psql -U postgres
```

## Deploy

Прод-чеклист (образ → Watchtower/COI → Caddy → web): **[deploy/README.md](deploy/README.md)**.

* `docker login -u alanextar`
* Пароль тут [DOCKER_HUB_ALANEXTAR](https://github.com/organizations/TarasovMilkovNazarov/settings/variables/actions)
* `docker build -t alanextar/gptipsbot:latest -t alanextar/gptipsbot:%НоваяВерсию% .`
* `docker image push alanextar/gptipsbot`
* `docker image push alanextar/gptipsbot:%НоваяВерсию%`
* Или push в ветку `release` — GitHub Actions соберёт образ **вместе с web UI**
### Tips
#### 

#### Автозапускалка контейнеров от яндекса
* Логи смотреть тут `sudo journalctl -n 11 -r -u yc-container-daemon`

##### adduser
* `sudo useradd -m -d /home/$username -s /bin/bash $username`
* `sudo su - $username`
* `mkdir .ssh`
* `touch .ssh/authorized_keys`
* `echo $ssh_key > /home/$username/.ssh/authorized_keys`
* `chmod 700 ~/.ssh`
* `chmod 600 ~/.ssh/authorized_keys`
* `sudo usermod -aG sudo $username`
* `sudo usermod -aG docker $username`

##### crontab
* Сохранить скрипт который надо запускать и сделать его запускаемым `chmod +x backup.sh`
* Добавить запуск скрипта по рассписанию. `crontab -e` и вписать свой запуск например `0 2 * * * /home/milkov/scripts/backup.sh` - вызывать каждый день в 2 часа ночи скрипт backup.sh
* Если сохранилось успешно в консоли будет следующий вывод `crontab: installing new crontab`
* Посмотреть что сейчас находится в crontab можно через `crontab -l`
* Если скрипт требует sudo прав, делать всё тоже самое только с припиской `sudo`
#### Containers
##### BD
* Данные БД по дефолту хранятся здесь `/var/lib/postgresql/data`
###### BACKUP
```docker exec -t gptipsbot-bd pg_dumpall -c -U postgres > dump_`date +%d-%m-%Y"_"%H_%M_%S`.sql```
###### RESTORE
```cat your_dump.sql | docker exec -i gptipsbot-bd psql -U postgres```
###### list of db with size
```
docker exec -it gptipsbot-bd psql -U postgres -c '\l+'
```
###### multicommand
```
docker exec -i 1d3350fd80f7 sh <<-EOF
   pg_dump --schema-only -U postgres gptips > schema.sql
   dropdb -U postgres "gptips"
   createdb -U postgres "gptips"
   psql -U postgres "gptips" < schema.sql
EOF
```
###### enter to psql
```
docker exec -it %containerID% psql -U postgres
```
after execute this command cmd will look like this `postgres=#`
###### list of database
```
\l
```
###### connect to gptips db
```
\c gptips
```
after execute this command cmd will look like this `gptips-#`
###### list of tables ()
```
\dt
```
`\dt+` for additional infos
