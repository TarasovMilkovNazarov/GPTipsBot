# GPTipsBot Web UI

ChatGPT-like web frontend for GPTipsBot (inspired by bota.chat).

## Dev

1. Run the ASP.NET API (`GPTipsBot`) on port 5000 (or update `vite.config.ts` proxy).
2. In this folder:

```bash
npm install
npm run dev
```

Open http://localhost:5173 — API calls proxy to `/api`.

## Production build

```bash
npm install
npm run build
```

Output goes to `GPTipsBot/wwwroot` and is served by the ASP.NET host.

## Features

- Guest session (cookie) with reduced free quotas
- Telegram Login Widget → same user/wallet as the bot
- Streaming chat (SSE), model picker, conversation history
- Image generation, OCR upload, voice STT
- RU/EN UI, image presets, quick-action pills
