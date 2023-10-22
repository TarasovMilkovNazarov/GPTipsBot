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
#### Containers
##### BD
* Данные по дефолту лежат здесь `/var/lib/postgresql/data`
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