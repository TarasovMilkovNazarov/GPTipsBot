import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import {
  api,
  type ChatMessage,
  type Conversation,
  type GptModel,
  type ImagePreset,
  type Me,
} from './api'
import { t, type Lang } from './i18n'

declare global {
  interface Window {
    onTelegramAuth?: (user: Record<string, unknown>) => void
  }
}

type Mode = 'chat' | 'image' | 'ocr'
type Theme = 'light' | 'dark'

const THEME_KEY = 'gptips_theme'

function readStoredTheme(): Theme {
  const stored = localStorage.getItem(THEME_KEY)
  if (stored === 'light' || stored === 'dark') return stored
  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
}

function applyTheme(theme: Theme) {
  document.documentElement.setAttribute('data-theme', theme)
}

export default function App() {
  const [lang, setLang] = useState<Lang>(() =>
    navigator.language.toLowerCase().startsWith('ru') ? 'ru' : 'en',
  )
  const [theme, setTheme] = useState<Theme>(() => {
    const initial = readStoredTheme()
    applyTheme(initial)
    return initial
  })
  const [me, setMe] = useState<Me | null>(null)
  const [models, setModels] = useState<GptModel[]>([])
  const [conversations, setConversations] = useState<Conversation[]>([])
  const [presets, setPresets] = useState<ImagePreset[]>([])
  const [botUsername, setBotUsername] = useState('GPTipsBot')
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [input, setInput] = useState('')
  const [contextId, setContextId] = useState<number | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [mode, setMode] = useState<Mode>('chat')
  const [historyOpen, setHistoryOpen] = useState(false)
  const [recording, setRecording] = useState(false)
  const [menuOpenId, setMenuOpenId] = useState<number | null>(null)
  const [renamingId, setRenamingId] = useState<number | null>(null)
  const [renameValue, setRenameValue] = useState('')
  const mediaRecorderRef = useRef<MediaRecorder | null>(null)
  const chunksRef = useRef<Blob[]>([])
  const fileRef = useRef<HTMLInputElement>(null)
  const bottomRef = useRef<HTMLDivElement>(null)
  const menuRef = useRef<HTMLDivElement>(null)
  const skipRenameBlurRef = useRef(false)

  const refreshMe = useCallback(async () => {
    const profile = await api.me()
    setMe(profile)
    return profile
  }, [])

  const refreshConversations = useCallback(async () => {
    try {
      setConversations(await api.conversations())
    } catch {
      /* guest first load */
    }
  }, [])

  useEffect(() => {
    ;(async () => {
      try {
        const [guest, modelData, presetData, cfg] = await Promise.all([
          api.ensureGuest(),
          api.models(),
          api.presets(),
          api.publicConfig(),
        ])
        setMe(guest)
        if (guest.isGuest && guest.id < 0) {
          localStorage.setItem('gptips_guest_id', String(guest.id))
        } else {
          localStorage.removeItem('gptips_guest_id')
        }
        setModels(modelData.models)
        setPresets(presetData)
        setBotUsername(cfg.botUsername)
        await refreshConversations()
      } catch (e) {
        setError(e instanceof Error ? e.message : 'Failed to start session')
      }
    })()
  }, [refreshConversations])

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages, busy])

  useEffect(() => {
    applyTheme(theme)
    localStorage.setItem(THEME_KEY, theme)
  }, [theme])

  useEffect(() => {
    if (menuOpenId == null) return
    const onPointerDown = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        setMenuOpenId(null)
      }
    }
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setMenuOpenId(null)
    }
    document.addEventListener('mousedown', onPointerDown)
    document.addEventListener('keydown', onKey)
    return () => {
      document.removeEventListener('mousedown', onPointerDown)
      document.removeEventListener('keydown', onKey)
    }
  }, [menuOpenId])

  useEffect(() => {
    window.onTelegramAuth = async (user) => {
      try {
        const storedGuest = Number(localStorage.getItem('gptips_guest_id') || '')
        const profile = await api.telegramLogin({
          id: user.id,
          firstName: user.first_name,
          lastName: user.last_name,
          username: user.username,
          photoUrl: user.photo_url,
          authDate: user.auth_date,
          hash: user.hash,
          previousGuestId: Number.isFinite(storedGuest) && storedGuest < 0 ? storedGuest : undefined,
        })
        localStorage.removeItem('gptips_guest_id')
        setMe(profile)
        await refreshConversations()
      } catch (e) {
        setError(e instanceof Error ? e.message : 'Telegram login failed')
      }
    }
    return () => {
      delete window.onTelegramAuth
    }
  }, [refreshConversations])

  useEffect(() => {
    const existing = document.getElementById('telegram-login-script')
    if (existing) existing.remove()
    const script = document.createElement('script')
    script.id = 'telegram-login-script'
    script.src = 'https://telegram.org/js/telegram-widget.js?22'
    script.async = true
    script.setAttribute('data-telegram-login', botUsername)
    script.setAttribute('data-size', 'medium')
    script.setAttribute('data-radius', '18')
    script.setAttribute('data-onauth', 'onTelegramAuth(user)')
    script.setAttribute('data-request-access', 'write')
    const host = document.getElementById('tg-login-host')
    host?.appendChild(script)
  }, [botUsername, me?.isGuest])

  const preferredModel = useMemo(
    () => models.find((m) => m.id === me?.model.id) ?? models[0],
    [models, me],
  )

  async function startNewChat() {
    setMessages([])
    setContextId(null)
    setMode('chat')
    setInput('')
    setError(null)
    setHistoryOpen(false)
  }

  async function openConversation(id: number) {
    setBusy(true)
    setError(null)
    setMenuOpenId(null)
    try {
      const data = await api.conversation(id)
      setContextId(id)
      setMessages(
        data.messages.map((m) => ({
          id: m.id,
          role: (m.role === 'assistant' ? 'assistant' : 'user') as ChatMessage['role'],
          text: m.text,
        })),
      )
      setMode('chat')
      setHistoryOpen(false)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load chat')
    } finally {
      setBusy(false)
    }
  }

  async function renameConversation(id: number, title: string) {
    const trimmed = title.trim()
    if (!trimmed) return
    try {
      const updated = await api.renameConversation(id, trimmed)
      setConversations((prev) =>
        prev.map((c) => (c.id === id ? { ...c, title: updated.title } : c)),
      )
      setRenamingId(null)
      setMenuOpenId(null)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Rename failed')
    }
  }

  async function togglePin(c: Conversation) {
    try {
      const updated = await api.pinConversation(c.id, !c.pinned)
      setConversations((prev) => {
        const next = prev.map((item) =>
          item.id === c.id ? { ...item, pinned: updated.pinned } : item,
        )
        return [...next].sort((a, b) => {
          if (!!a.pinned !== !!b.pinned) return a.pinned ? -1 : 1
          return new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime()
        })
      })
      setMenuOpenId(null)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Pin failed')
    }
  }

  async function deleteConversation(id: number) {
    if (!window.confirm(t(lang, 'deleteConfirm'))) return
    try {
      await api.deleteConversation(id)
      setConversations((prev) => prev.filter((c) => c.id !== id))
      if (contextId === id) {
        setMessages([])
        setContextId(null)
      }
      setMenuOpenId(null)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Delete failed')
    }
  }

  async function onModelChange(modelId: string) {
    await api.setModel(modelId)
    await refreshMe()
  }

  async function sendChat(text: string) {
    const trimmed = text.trim()
    if (!trimmed || busy) return
    setBusy(true)
    setError(null)
    setInput('')
    setMessages((prev) => [...prev, { role: 'user', text: trimmed }, { role: 'assistant', text: '' }])
    const newConversation = contextId == null
    try {
      await api.streamChat(trimmed, newConversation, contextId, (type, payload) => {
        if (type === 'start' && typeof payload.contextId === 'number') {
          setContextId(payload.contextId)
        }
        if (type === 'token' && typeof payload.text === 'string') {
          setMessages((prev) => {
            const copy = [...prev]
            const last = copy[copy.length - 1]
            if (last?.role === 'assistant') {
              copy[copy.length - 1] = { ...last, text: last.text + payload.text }
            }
            return copy
          })
        }
        if (type === 'done' && typeof payload.contextId === 'number') {
          setContextId(payload.contextId)
        }
        if (type === 'error') {
          setError(String(payload.message || 'Chat error'))
        }
      })
      await refreshMe()
      await refreshConversations()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Chat failed')
    } finally {
      setBusy(false)
    }
  }

  async function generateImage(prompt: string) {
    const trimmed = prompt.trim()
    if (!trimmed || busy) return
    setBusy(true)
    setError(null)
    setMessages((prev) => [...prev, { role: 'user', text: trimmed }])
    try {
      const result = await api.generateImage(trimmed)
      setMessages((prev) => [
        ...prev,
        {
          role: 'assistant',
          text: `Image generated (${result.starsCharged}⭐)`,
          imageUrl: `data:${result.mimeType};base64,${result.base64}`,
        },
      ])
      await refreshMe()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Image generation failed')
    } finally {
      setBusy(false)
      setMode('chat')
    }
  }

  async function onOcrFile(file: File) {
    setBusy(true)
    setError(null)
    setMessages((prev) => [...prev, { role: 'user', text: `OCR: ${file.name}` }])
    try {
      const { text } = await api.ocr(file)
      setMessages((prev) => [...prev, { role: 'assistant', text: text || '(no text found)' }])
      await refreshMe()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'OCR failed')
    } finally {
      setBusy(false)
      setMode('chat')
    }
  }

  async function toggleVoice() {
    if (recording) {
      mediaRecorderRef.current?.stop()
      setRecording(false)
      return
    }
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true })
      const recorder = new MediaRecorder(stream)
      chunksRef.current = []
      recorder.ondataavailable = (e) => {
        if (e.data.size > 0) chunksRef.current.push(e.data)
      }
      recorder.onstop = async () => {
        stream.getTracks().forEach((tr) => tr.stop())
        const blob = new Blob(chunksRef.current, { type: 'audio/webm' })
        setBusy(true)
        try {
          const { text } = await api.stt(blob)
          if (text) {
            setInput(text)
            await sendChat(text)
          }
        } catch (e) {
          setError(e instanceof Error ? e.message : 'Speech recognition failed')
        } finally {
          setBusy(false)
        }
      }
      mediaRecorderRef.current = recorder
      recorder.start()
      setRecording(true)
    } catch {
      setError('Microphone permission denied')
    }
  }

  async function onSubmit() {
    if (mode === 'image') {
      await generateImage(input)
      setInput('')
      return
    }
    await sendChat(input)
  }

  async function logout() {
    await api.logout()
    const guest = await api.ensureGuest()
    setMe(guest)
    if (guest.isGuest && guest.id < 0) {
      localStorage.setItem('gptips_guest_id', String(guest.id))
    }
    setMessages([])
    setContextId(null)
    await refreshConversations()
  }

  const showHero = messages.length === 0

  return (
    <div className="app">
      <aside className="sidebar">
        <button title={t(lang, 'history')} onClick={() => setHistoryOpen((v) => !v)} type="button">
          ☰
        </button>
        <button title={t(lang, 'newChat')} onClick={startNewChat} type="button" aria-label={t(lang, 'newChat')}>
          +
        </button>
        <div className="brand-dot">G</div>
      </aside>

      <div className="main">
        <header className="topbar">
          <div className="topbar-left">
            <strong>GPTipsBot</strong>
            <button className="ghost" type="button" onClick={() => setLang(lang === 'en' ? 'ru' : 'en')}>
              {lang.toUpperCase()}
            </button>
            <button
              className="ghost theme-toggle"
              type="button"
              title={theme === 'dark' ? t(lang, 'themeLight') : t(lang, 'themeDark')}
              aria-label={theme === 'dark' ? t(lang, 'themeLight') : t(lang, 'themeDark')}
              onClick={() => setTheme((v) => (v === 'dark' ? 'light' : 'dark'))}
            >
              {theme === 'dark' ? 'Light' : 'Dark'}
            </button>
          </div>
          <div className="topbar-right">
            {me && (
              <>
                <span className="stat">
                  {me.isGuest ? t(lang, 'guest') : me.firstName} · {me.stars.toFixed(1)} {t(lang, 'stars')}
                </span>
                <span className="stat">
                  {t(lang, 'freeGpt')}: {me.free.gpt}
                </span>
              </>
            )}
            {me?.isGuest ? <div id="tg-login-host" /> : (
              <button className="pill" type="button" onClick={logout}>
                {t(lang, 'logout')}
              </button>
            )}
          </div>
        </header>

        <div className={`drawer-backdrop ${historyOpen ? 'open' : ''}`} onClick={() => setHistoryOpen(false)} />

        <div className="workspace">
          <aside className={`history ${historyOpen ? 'open' : ''}`}>
            <h3>{t(lang, 'history')}</h3>
            {conversations.length === 0 && <div className="stat">{t(lang, 'noHistory')}</div>}
            {conversations.map((c) => (
              <div
                key={c.id}
                className={`history-item ${contextId === c.id ? 'active' : ''} ${menuOpenId === c.id ? 'menu-open' : ''}`}
              >
                {renamingId === c.id ? (
                  <form
                    className="history-rename"
                    onSubmit={(e) => {
                      e.preventDefault()
                      void renameConversation(c.id, renameValue)
                    }}
                  >
                    <input
                      autoFocus
                      value={renameValue}
                      onChange={(e) => setRenameValue(e.target.value)}
                      onBlur={() => {
                        if (skipRenameBlurRef.current) {
                          skipRenameBlurRef.current = false
                          return
                        }
                        void renameConversation(c.id, renameValue)
                      }}
                      onKeyDown={(e) => {
                        if (e.key === 'Escape') {
                          e.preventDefault()
                          skipRenameBlurRef.current = true
                          setRenamingId(null)
                        }
                      }}
                      aria-label={t(lang, 'rename')}
                    />
                  </form>
                ) : (
                  <button
                    type="button"
                    className="history-item-main"
                    onClick={() => openConversation(c.id)}
                  >
                    <span className="history-title">{c.title}</span>
                    <small>{new Date(c.updatedAt).toLocaleString()}</small>
                  </button>
                )}

                <div className="history-item-actions" ref={menuOpenId === c.id ? menuRef : undefined}>
                  {c.pinned && renamingId !== c.id && (
                    <span className="history-pin-icon" aria-hidden title={t(lang, 'pinChat')}>
                      <PinIcon />
                    </span>
                  )}
                  <button
                    type="button"
                    className="history-ellipsis"
                    aria-label={t(lang, 'chatMenu')}
                    aria-expanded={menuOpenId === c.id}
                    onClick={(e) => {
                      e.stopPropagation()
                      setMenuOpenId((id) => (id === c.id ? null : c.id))
                    }}
                  >
                    <EllipsisIcon />
                  </button>

                  {menuOpenId === c.id && (
                    <div className="chat-menu" role="menu">
                      <button
                        type="button"
                        role="menuitem"
                        onClick={() => {
                          setRenamingId(c.id)
                          setRenameValue(c.title)
                          setMenuOpenId(null)
                        }}
                      >
                        <PencilIcon />
                        {t(lang, 'rename')}
                      </button>
                      <div className="chat-menu-sep" />
                      <button type="button" role="menuitem" onClick={() => void togglePin(c)}>
                        <PinIcon />
                        {c.pinned ? t(lang, 'unpinChat') : t(lang, 'pinChat')}
                      </button>
                      <button
                        type="button"
                        role="menuitem"
                        className="danger"
                        onClick={() => void deleteConversation(c.id)}
                      >
                        <TrashIcon />
                        {t(lang, 'deleteChat')}
                      </button>
                    </div>
                  )}
                </div>
              </div>
            ))}
          </aside>

          <section className="chat-pane">
            {showHero && (
              <div className="hero">
                <h1>{t(lang, 'howCanIHelp')}</h1>
              </div>
            )}

            <div className="messages">
              {messages.map((m, idx) => (
                <div key={idx} className={`bubble ${m.role}`}>
                  {m.text}
                  {m.imageUrl && <img src={m.imageUrl} alt="generated" />}
                </div>
              ))}
              <div ref={bottomRef} />
            </div>

            {error && <div className="error">{error}</div>}

            <div className="composer-wrap">
              <div className="composer">
                <textarea
                  value={input}
                  placeholder={
                    recording
                      ? t(lang, 'recording')
                      : mode === 'image'
                        ? t(lang, 'imagePrompt')
                        : t(lang, 'typeMessage')
                  }
                  onChange={(e) => setInput(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' && !e.shiftKey) {
                      e.preventDefault()
                      void onSubmit()
                    }
                  }}
                  disabled={busy || recording}
                />
                <div className="composer-bar">
                  <button
                    className="icon-btn"
                    type="button"
                    title={t(lang, 'attach')}
                    onClick={() => {
                      setMode('ocr')
                      fileRef.current?.click()
                    }}
                  >
                    +
                  </button>
                  <select
                    value={preferredModel?.id || ''}
                    onChange={(e) => void onModelChange(e.target.value)}
                    aria-label={t(lang, 'model')}
                  >
                    {models.map((m) => (
                      <option key={m.id} value={m.id}>
                        {m.emoji} {m.displayName} · {m.starsCost}⭐
                      </option>
                    ))}
                  </select>
                  <div className="spacer" />
                  <button
                    className={`icon-btn ${recording ? 'primary' : ''}`}
                    type="button"
                    onClick={() => void toggleVoice()}
                    title={t(lang, 'talkByVoice')}
                  >
                    {recording ? '■' : '🎤'}
                  </button>
                  <button
                    className="icon-btn primary"
                    type="button"
                    disabled={busy || (!input.trim() && !recording)}
                    onClick={() => void onSubmit()}
                  >
                    ↑
                  </button>
                </div>
              </div>

              <div className="actions">
                <button className="pill" type="button" onClick={() => setMode('image')}>
                  {t(lang, 'createImage')}
                </button>
                <button className="pill" type="button" onClick={() => void toggleVoice()}>
                  {t(lang, 'talkByVoice')}
                </button>
                <button
                  className="pill"
                  type="button"
                  onClick={() => {
                    setMode('ocr')
                    fileRef.current?.click()
                  }}
                >
                  {t(lang, 'ocr')}
                </button>
                <button
                  className="pill"
                  type="button"
                  onClick={() => {
                    setMode('chat')
                    setInput(lang === 'ru' ? 'Переведи на английский: ' : 'Translate to English: ')
                  }}
                >
                  {t(lang, 'translate')}
                </button>
              </div>
            </div>

            {showHero && (
              <>
                <div className="presets">
                  <h2>{t(lang, 'createImages')}</h2>
                  <div className="preset-grid">
                    {presets.map((p) => (
                      <button
                        key={p.id}
                        type="button"
                        className="preset-card"
                        onClick={() => {
                          setMode('image')
                          setInput(p.prompt)
                        }}
                      >
                        <strong>{p.title}</strong>
                        <span>{p.prompt.slice(0, 72)}…</span>
                      </button>
                    ))}
                  </div>
                </div>

                <div className="presets">
                  <h2>{t(lang, 'quickTasks')}</h2>
                  <div className="preset-grid">
                    <button
                      type="button"
                      className="preset-card"
                      onClick={() => setInput(lang === 'ru' ? 'Объясни простыми словами: ' : 'Explain simply: ')}
                    >
                      <strong>{lang === 'ru' ? 'Объяснить' : 'Explain'}</strong>
                      <span>{lang === 'ru' ? 'Сложные темы простыми словами' : 'Hard topics in plain words'}</span>
                    </button>
                    <button
                      type="button"
                      className="preset-card"
                      onClick={() => setInput(lang === 'ru' ? 'Напиши код: ' : 'Write code: ')}
                    >
                      <strong>{lang === 'ru' ? 'Код' : 'Code'}</strong>
                      <span>{lang === 'ru' ? 'Скрипты и отладка' : 'Scripts and debugging'}</span>
                    </button>
                    <button
                      type="button"
                      className="preset-card"
                      onClick={() => setInput(lang === 'ru' ? 'Перепиши текст: ' : 'Rewrite this text: ')}
                    >
                      <strong>{lang === 'ru' ? 'Рерайт' : 'Rewrite'}</strong>
                      <span>{lang === 'ru' ? 'Письма и посты' : 'Emails and posts'}</span>
                    </button>
                  </div>
                </div>

                <section className="seo">
                  <h2>{t(lang, 'seoTitle')}</h2>
                  <p>{t(lang, 'seoBody')}</p>
                </section>
              </>
            )}
          </section>
        </div>
      </div>

      <input
        ref={fileRef}
        type="file"
        accept="image/*"
        hidden
        onChange={(e) => {
          const file = e.target.files?.[0]
          if (file) void onOcrFile(file)
          e.target.value = ''
        }}
      />
    </div>
  )
}

function EllipsisIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 16 16" fill="currentColor" aria-hidden>
      <circle cx="3" cy="8" r="1.5" />
      <circle cx="8" cy="8" r="1.5" />
      <circle cx="13" cy="8" r="1.5" />
    </svg>
  )
}

function PencilIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden>
      <path d="M12 20h9" />
      <path d="M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z" />
    </svg>
  )
}

function PinIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden>
      <path d="M12 17v5" />
      <path d="M9 10.5V4a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v6.5l2.5 2.5V15H6.5v-2l2.5-2.5z" />
    </svg>
  )
}

function TrashIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden>
      <path d="M3 6h18" />
      <path d="M8 6V4h8v2" />
      <path d="M19 6l-1 14H6L5 6" />
      <path d="M10 11v6M14 11v6" />
    </svg>
  )
}
