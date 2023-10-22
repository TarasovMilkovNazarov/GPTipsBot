# GPTipsBot

What is it?
Free telegram bot with ChatGPT integration and DALL-E without any subscriptions

* Board: https://trello.com/b/hyydi02P/chatgtp-telegram-bot
* Telegram bot:
  * PROD https://t.me/GPTipsBot
  * CLOUD https://t.me/GPTipBot
* Секреты: https://github.com/organizations/TarasovMilkovNazarov/settings/variables/actions
* Hosting: https://console.cloud.yandex.ru/folders/b1ghg7fp1esojrsq87tq
* Deploy:

### Tips
#### BashLinuxEtc
##### crontab
* Сохранить скрипт который надо запускать и сделать его запускаемым `chmod +x backup.sh`
* Добавить запуск скрипта по рассписанию. `crontab -e` и вписать свой запуск например `0 2 * * * /home/milkov/scripts/backup.sh` - вызывать каждый день в 2 часа ночи скрипт backup.sh
* Если сохранилось успешно в консоли будет следующий вывод `crontab: installing new crontab`
* Посмотреть что сейчас находится в crontab можно через `crontab -l`
* Если скрипт требует sudo прав, делать всё тоже самое только с припиской `sudo`
#### Containers
##### BD
* Данные по дефолту лежат здесь `/var/lib/postgresql/data`
###### BACKUP
```docker exec -t gptipsbot-bd pg_dumpall -c -U postgres > dump_`date +%d-%m-%Y"_"%H_%M_%S`.sql```
###### RESTORE
```cat your_dump.sql | docker exec -i gptipsbot-bd psql -U postgres```
###### list of db with size
```
docker exec -it 1d3350fd80f7 psql -U postgres -c '\l+'
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