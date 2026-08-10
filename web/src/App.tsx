import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import {
  api,
  type ChatMessage,
  type Conversation,
  type GptModel,
  type ImagePreset,
  type Me,
  type PaymentPackages,
} from './api'
import { t, type Lang } from './i18n'

declare global {
  interface Window {
    onTelegramAuth?: (user: Record<string, unknown>) => void
  }
}

type Mode = 'chat' | 'image' | 'ocr' | 'cabinet'
type UploadIntent = 'ocr' | 'promptFromImage'
type Theme = 'light' | 'dark'

const THEME_KEY = 'gptips_theme'
const PENDING_INVOICE_KEY = 'gptips_pending_invoice'

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
  const [paymentInfo, setPaymentInfo] = useState<PaymentPackages | null>(null)
  const [payingStars, setPayingStars] = useState<number | null>(null)
  const [paymentNotice, setPaymentNotice] = useState<'success' | 'pending' | 'failed' | null>(null)
  const [authOpen, setAuthOpen] = useState(false)
  const [authView, setAuthView] = useState<'method' | 'telegram' | 'email'>('method')
  const [authTab, setAuthTab] = useState<'login' | 'register' | 'confirm'>('login')
  const [quotaUpsell, setQuotaUpsell] = useState(false)
  const [authEmail, setAuthEmail] = useState('')
  const [authPassword, setAuthPassword] = useState('')
  const [authName, setAuthName] = useState('')
  const [authCode, setAuthCode] = useState('')
  const [authBusy, setAuthBusy] = useState(false)
  const [authHint, setAuthHint] = useState<string | null>(null)
  const mediaRecorderRef = useRef<MediaRecorder | null>(null)
  const chunksRef = useRef<Blob[]>([])
  const fileRef = useRef<HTMLInputElement>(null)
  const uploadIntentRef = useRef<UploadIntent>('ocr')
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
        const [guest, modelData, presetData, cfg, packages] = await Promise.all([
          api.ensureGuest(),
          api.models(),
          api.presets(),
          api.publicConfig(),
          api.paymentPackages().catch(() => null),
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
        setPaymentInfo(packages)
        await refreshConversations()

        const params = new URLSearchParams(window.location.search)
        const openCabinet = params.get('cabinet') === '1' || params.get('paid') === '1'
        const pendingRaw = localStorage.getItem(PENDING_INVOICE_KEY)
        const pendingId = pendingRaw ? Number(pendingRaw) : NaN
        if (openCabinet || Number.isFinite(pendingId)) {
          setMode('cabinet')
        }
        if (Number.isFinite(pendingId) && pendingId > 0 && !guest.isGuest) {
          try {
            const sync = await api.syncPayment(pendingId)
            if (sync.credited) {
              setMe(sync.me)
              localStorage.removeItem(PENDING_INVOICE_KEY)
              setPaymentNotice('success')
            } else {
              setPaymentNotice('pending')
            }
          } catch {
            setPaymentNotice('pending')
          }
        }
        if (openCabinet) {
          const url = new URL(window.location.href)
          url.searchParams.delete('cabinet')
          url.searchParams.delete('paid')
          window.history.replaceState({}, '', url.pathname + url.search + url.hash)
        }
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
        setAuthOpen(false)
        setAuthView('method')
        setError(null)
        window.location.reload()
      } catch (e) {
        setError(e instanceof Error ? e.message : 'Telegram login failed')
      }
    }
    return () => {
      delete window.onTelegramAuth
    }
  }, [refreshConversations])

  useEffect(() => {
    if (!authOpen || authView !== 'telegram') return
    const host = document.getElementById('tg-login-host')
    if (!host) return
    host.innerHTML = ''
    const script = document.createElement('script')
    script.id = 'telegram-login-script'
    script.src = 'https://telegram.org/js/telegram-widget.js?22'
    script.async = true
    script.setAttribute('data-telegram-login', botUsername)
    script.setAttribute('data-size', 'large')
    script.setAttribute('data-radius', '18')
    script.setAttribute('data-onauth', 'onTelegramAuth(user)')
    script.setAttribute('data-request-access', 'write')
    host.appendChild(script)
  }, [authOpen, authView, botUsername])

  function openAuth(
    view: 'method' | 'telegram' | 'email' = 'method',
    options?: { preferRegister?: boolean; restoreHint?: boolean },
  ) {
    setAuthOpen(true)
    setAuthView(view)
    setAuthTab(options?.preferRegister ? 'register' : 'login')
    setAuthHint(options?.restoreHint ? t(lang, 'authRestoreHint') : null)
    setError(null)
  }

  function promptGuestRegister() {
    setError(null)
    setQuotaUpsell(true)
  }

  function closeQuotaUpsell() {
    setQuotaUpsell(false)
  }

  function continueFromQuotaUpsell(view: 'telegram' | 'email') {
    setQuotaUpsell(false)
    openAuth(view, { preferRegister: true, restoreHint: true })
  }

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
    setQuotaUpsell(false)
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
    setQuotaUpsell(false)
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
          if (payload.code === 'quota' && payload.suggestRegister === true) {
            promptGuestRegister()
          } else {
            setError(String(payload.message || 'Chat error'))
            setQuotaUpsell(false)
          }
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

  async function onPromptFromImageFile(file: File) {
    setBusy(true)
    setError(null)
    setMessages((prev) => [
      ...prev,
      { role: 'user', text: `${t(lang, 'promptFromImage')}: ${file.name}` },
    ])
    try {
      const { text } = await api.promptFromImage(file)
      setMessages((prev) => [
        ...prev,
        { role: 'assistant', text: text || '(empty prompt)' },
      ])
      await refreshMe()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Prompt from image failed')
    } finally {
      setBusy(false)
      setMode('chat')
    }
  }

  function openImageUpload(intent: UploadIntent) {
    uploadIntentRef.current = intent
    setMode('ocr')
    fileRef.current?.click()
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
    setMode('chat')
    setPaymentNotice(null)
    await refreshConversations()
  }

  async function openCabinet() {
    setMode('cabinet')
    setHistoryOpen(false)
    setError(null)
    try {
      setPaymentInfo(await api.paymentPackages())
      await refreshMe()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load account')
    }
  }

  async function startTopUp(stars: number) {
    if (!me || me.isGuest) {
      openAuth('method')
      setError(t(lang, 'loginToTopUp'))
      return
    }
    setPayingStars(stars)
    setError(null)
    setPaymentNotice(null)
    try {
      const checkout = await api.createYooKassaPayment(stars)
      localStorage.setItem(PENDING_INVOICE_KEY, String(checkout.invoiceId))
      window.location.href = checkout.confirmationUrl
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Payment failed')
      setPayingStars(null)
    }
  }

  async function checkPendingPayment() {
    const pendingRaw = localStorage.getItem(PENDING_INVOICE_KEY)
    const pendingId = pendingRaw ? Number(pendingRaw) : NaN
    if (!Number.isFinite(pendingId) || pendingId <= 0) return
    setPaymentNotice('pending')
    try {
      const sync = await api.syncPayment(pendingId)
      if (sync.credited) {
        setMe(sync.me)
        localStorage.removeItem(PENDING_INVOICE_KEY)
        setPaymentNotice('success')
      } else {
        setPaymentNotice('failed')
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Sync failed')
      setPaymentNotice('failed')
    }
  }

  function guestIdForMerge(): number | undefined {
    const stored = Number(localStorage.getItem('gptips_guest_id') || '')
    return Number.isFinite(stored) && stored < 0 ? stored : undefined
  }

  async function afterEmailAuth(profile: Me) {
    localStorage.removeItem('gptips_guest_id')
    setMe(profile)
    setAuthOpen(false)
    setAuthView('method')
    setAuthPassword('')
    setAuthCode('')
    setAuthHint(null)
    setError(null)
    // Full reload so cookie session + topbar match the signed-in user.
    window.location.reload()
  }

  async function submitEmailLogin() {
    setAuthBusy(true)
    setError(null)
    setAuthHint(null)
    try {
      const profile = await api.emailLogin(authEmail.trim(), authPassword, guestIdForMerge())
      await afterEmailAuth(profile)
    } catch (e) {
      const message = e instanceof Error ? e.message : 'Login failed'
      setError(message)
      if (message.toLowerCase().includes('not confirmed')) {
        setAuthView('email')
        setAuthTab('confirm')
      }
    } finally {
      setAuthBusy(false)
    }
  }

  async function submitEmailRegister() {
    setAuthBusy(true)
    setError(null)
    setAuthHint(null)
    try {
      const res = await api.emailRegister(authEmail.trim(), authPassword, authName.trim() || undefined)
      setAuthTab('confirm')
      setAuthHint(
        res.devCode
          ? `${t(lang, 'codeSent')}: ${res.devCode}`
          : t(lang, 'codeSent'),
      )
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Register failed')
    } finally {
      setAuthBusy(false)
    }
  }

  async function submitEmailConfirm() {
    setAuthBusy(true)
    setError(null)
    try {
      const profile = await api.emailConfirm(authEmail.trim(), authCode.trim(), guestIdForMerge())
      await afterEmailAuth(profile)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Confirm failed')
    } finally {
      setAuthBusy(false)
    }
  }

  async function resendEmailCode() {
    setAuthBusy(true)
    setError(null)
    try {
      const res = await api.emailResend(authEmail.trim())
      setAuthHint(
        res.devCode
          ? `${t(lang, 'codeSent')}: ${res.devCode}`
          : t(lang, 'codeSent'),
      )
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Resend failed')
    } finally {
      setAuthBusy(false)
    }
  }

  const showHero = messages.length === 0 && mode !== 'cabinet'
  const telegramBotUrl = `https://t.me/${botUsername.replace(/^@/, '')}`

  return (
    <div className="app">
      <aside className="sidebar">
        <button title={t(lang, 'history')} onClick={() => setHistoryOpen((v) => !v)} type="button">
          ☰
        </button>
        <button title={t(lang, 'newChat')} onClick={startNewChat} type="button" aria-label={t(lang, 'newChat')}>
          +
        </button>
        <button title={t(lang, 'cabinet')} onClick={() => void openCabinet()} type="button" aria-label={t(lang, 'cabinet')}>
          ₽
        </button>
        <a
          className="sidebar-link"
          href={telegramBotUrl}
          target="_blank"
          rel="noreferrer"
          title={t(lang, 'openTelegram')}
          aria-label={t(lang, 'openTelegram')}
        >
          TG
        </a>
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
            <a className="pill telegram-link" href={telegramBotUrl} target="_blank" rel="noreferrer">
              Telegram
            </a>
          </div>
          <div className="topbar-right">
            {me && (
              <>
                <button className="stat balance-chip" type="button" onClick={() => void openCabinet()}>
                  {me.isGuest ? t(lang, 'guest') : me.firstName} · {me.stars.toFixed(1)} {t(lang, 'stars')}
                </button>
                <span className="stat">
                  {t(lang, 'freeGpt')}: {me.free.gpt}
                </span>
              </>
            )}
            {me?.isGuest ? (
              <button className="pill primary" type="button" onClick={() => openAuth('method')}>
                {t(lang, 'login')}
              </button>
            ) : (
              <button className="pill" type="button" onClick={logout}>
                {t(lang, 'logout')}
              </button>
            )}
          </div>
        </header>

        {quotaUpsell && me?.isGuest && (
          <div className="auth-backdrop upsell-backdrop" onClick={closeQuotaUpsell}>
            <div className="auth-modal upsell-modal" onClick={(e) => e.stopPropagation()} role="dialog" aria-modal="true">
              <div className="upsell-badge">{lang === 'ru' ? 'Лимит исчерпан' : 'Limit reached'}</div>
              <h2 className="upsell-title">{t(lang, 'quotaUpsellTitle')}</h2>
              <p className="upsell-lead">{t(lang, 'quotaUpsellLead')}</p>
              <ul className="upsell-benefits">
                <li>{t(lang, 'quotaUpsellBenefitGpt')}</li>
                <li>{t(lang, 'quotaUpsellBenefitImages')}</li>
                <li>{t(lang, 'quotaUpsellBenefitSync')}</li>
              </ul>
              <div className="auth-methods">
                <button
                  className="auth-method telegram"
                  type="button"
                  onClick={() => continueFromQuotaUpsell('telegram')}
                >
                  {t(lang, 'authViaTelegram')}
                </button>
                <button
                  className="auth-method email"
                  type="button"
                  onClick={() => continueFromQuotaUpsell('email')}
                >
                  {t(lang, 'authViaEmail')}
                </button>
              </div>
              <button className="ghost upsell-dismiss" type="button" onClick={closeQuotaUpsell}>
                {t(lang, 'quotaUpsellContinue')}
              </button>
            </div>
          </div>
        )}

        {authOpen && (
          <div className="auth-backdrop" onClick={() => !authBusy && setAuthOpen(false)}>
            <div className="auth-modal" onClick={(e) => e.stopPropagation()}>
              <div className="auth-modal-head">
                <h2>{t(lang, 'authTitle')}</h2>
                {authView !== 'method' && (
                  <button className="ghost" type="button" onClick={() => { setAuthView('method'); setAuthHint(null); setError(null) }}>
                    {t(lang, 'authBack')}
                  </button>
                )}
              </div>

              {authView === 'method' && (
                <div className="auth-methods">
                  {authHint && <p className="auth-choose">{authHint}</p>}
                  <p className="auth-choose">{t(lang, 'authChoose')}</p>
                  <button className="auth-method telegram" type="button" onClick={() => setAuthView('telegram')}>
                    {t(lang, 'authViaTelegram')}
                  </button>
                  <button
                    className="auth-method email"
                    type="button"
                    onClick={() => {
                      setAuthView('email')
                      setAuthTab(authTab === 'register' ? 'register' : 'login')
                    }}
                  >
                    {t(lang, 'authViaEmail')}
                  </button>
                </div>
              )}

              {authView === 'telegram' && (
                <div className="auth-telegram">
                  <p className="auth-choose">{t(lang, 'authViaTelegram')}</p>
                  <div id="tg-login-host" className="tg-login-host" />
                </div>
              )}

              {authView === 'email' && (
                <>
                  <div className="auth-tabs">
                    <button
                      type="button"
                      className={authTab === 'login' ? 'active' : ''}
                      onClick={() => setAuthTab('login')}
                    >
                      {t(lang, 'emailLogin')}
                    </button>
                    <button
                      type="button"
                      className={authTab === 'register' ? 'active' : ''}
                      onClick={() => setAuthTab('register')}
                    >
                      {t(lang, 'emailRegister')}
                    </button>
                    <button
                      type="button"
                      className={authTab === 'confirm' ? 'active' : ''}
                      onClick={() => setAuthTab('confirm')}
                    >
                      {t(lang, 'emailConfirm')}
                    </button>
                  </div>

                  {(authTab === 'login' || authTab === 'register') && (
                    <div className="auth-form">
                      {authTab === 'register' && (
                        <input
                          value={authName}
                          onChange={(e) => setAuthName(e.target.value)}
                          placeholder={t(lang, 'namePlaceholder')}
                          autoComplete="name"
                        />
                      )}
                      <input
                        type="email"
                        value={authEmail}
                        onChange={(e) => setAuthEmail(e.target.value)}
                        placeholder={t(lang, 'emailPlaceholder')}
                        autoComplete="email"
                      />
                      <input
                        type="password"
                        value={authPassword}
                        onChange={(e) => setAuthPassword(e.target.value)}
                        placeholder={t(lang, 'passwordPlaceholder')}
                        autoComplete={authTab === 'login' ? 'current-password' : 'new-password'}
                      />
                      <button
                        className="primary"
                        type="button"
                        disabled={authBusy || !authEmail.trim() || authPassword.length < 6}
                        onClick={() => void (authTab === 'login' ? submitEmailLogin() : submitEmailRegister())}
                      >
                        {authTab === 'login' ? t(lang, 'emailLogin') : t(lang, 'sendCode')}
                      </button>
                    </div>
                  )}

                  {authTab === 'confirm' && (
                    <div className="auth-form">
                      <input
                        type="email"
                        value={authEmail}
                        onChange={(e) => setAuthEmail(e.target.value)}
                        placeholder={t(lang, 'emailPlaceholder')}
                        autoComplete="email"
                      />
                      <input
                        value={authCode}
                        onChange={(e) => setAuthCode(e.target.value)}
                        placeholder={t(lang, 'codePlaceholder')}
                        inputMode="numeric"
                        maxLength={6}
                      />
                      <button
                        className="primary"
                        type="button"
                        disabled={authBusy || !authEmail.trim() || authCode.trim().length < 4}
                        onClick={() => void submitEmailConfirm()}
                      >
                        {t(lang, 'confirmCode')}
                      </button>
                      <button className="ghost" type="button" disabled={authBusy || !authEmail.trim()} onClick={() => void resendEmailCode()}>
                        {t(lang, 'resendCode')}
                      </button>
                    </div>
                  )}
                </>
              )}

              {authHint && <div className="auth-hint">{authHint}</div>}
              {error && <div className="error">{error}</div>}
            </div>
          </div>
        )}

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
            {mode === 'cabinet' ? (
              <div className="cabinet">
                <div className="cabinet-head">
                  <h1>{t(lang, 'cabinetTitle')}</h1>
                  <button className="ghost" type="button" onClick={() => setMode('chat')}>
                    {t(lang, 'backToChat')}
                  </button>
                </div>

                {me && (
                  <div className="cabinet-card">
                    <div className="cabinet-label">{t(lang, 'balance')}</div>
                    <div className="cabinet-balance">
                      {me.stars.toFixed(1)} <span>{t(lang, 'stars')}</span>
                    </div>
                    <div className="cabinet-user">
                      {me.isGuest ? t(lang, 'guest') : [me.firstName, me.lastName].filter(Boolean).join(' ')}
                    </div>
                  </div>
                )}

                {me && (
                  <div className="cabinet-card">
                    <h2>{t(lang, 'freeQuotas')}</h2>
                    <div className="quota-grid">
                      <div><strong>{me.free.gpt}</strong><span>{t(lang, 'freeGpt')}</span></div>
                      <div><strong>{me.free.images}</strong><span>{t(lang, 'images')}</span></div>
                      <div><strong>{me.free.ocr}</strong><span>{t(lang, 'ocr')}</span></div>
                      <div><strong>{me.free.animations}</strong><span>{t(lang, 'animations')}</span></div>
                      <div><strong>{me.free.summaries}</strong><span>{t(lang, 'summaries')}</span></div>
                    </div>
                  </div>
                )}

                <div className="cabinet-card">
                  <h2>{t(lang, 'topUp')}</h2>
                  <p className="cabinet-hint">{t(lang, 'topUpHint')}</p>

                  {paymentNotice === 'success' && (
                    <div className="payment-banner ok">{t(lang, 'paymentSuccess')}</div>
                  )}
                  {paymentNotice === 'pending' && (
                    <div className="payment-banner">
                      {t(lang, 'paymentPending')}
                      <button className="ghost" type="button" onClick={() => void checkPendingPayment()}>
                        {t(lang, 'checkPayment')}
                      </button>
                    </div>
                  )}
                  {paymentNotice === 'failed' && (
                    <div className="payment-banner warn">
                      {t(lang, 'paymentFailed')}
                      <button className="ghost" type="button" onClick={() => void checkPendingPayment()}>
                        {t(lang, 'checkPayment')}
                      </button>
                    </div>
                  )}

                  {me?.isGuest ? (
                    <div className="auth-methods">
                      <div className="cabinet-hint">{t(lang, 'loginToTopUp')}</div>
                      <button className="primary" type="button" onClick={() => openAuth('method')}>
                        {t(lang, 'login')}
                      </button>
                    </div>
                  ) : !paymentInfo?.enabled ? (
                    <div className="cabinet-hint">{t(lang, 'paymentsDisabled')}</div>
                  ) : (
                    <div className="package-grid">
                      {paymentInfo.packages.map((p) => (
                        <button
                          key={p.stars}
                          type="button"
                          className="package-card"
                          disabled={payingStars !== null}
                          onClick={() => void startTopUp(p.stars)}
                        >
                          <strong>{p.stars} {t(lang, 'stars')}</strong>
                          <span>{p.rub} ₽</span>
                          <em>{payingStars === p.stars ? t(lang, 'paying') : t(lang, 'pay')}</em>
                        </button>
                      ))}
                    </div>
                  )}
                </div>

                {error && <div className="error">{error}</div>}

                <a className="pill telegram-link cabinet-telegram" href={telegramBotUrl} target="_blank" rel="noreferrer">
                  {t(lang, 'openTelegram')} · @{botUsername.replace(/^@/, '')}
                </a>
              </div>
            ) : (
              <>
            {showHero && (
              <div className="hero">
                <h1>{t(lang, 'howCanIHelp')}</h1>
                <a className="pill telegram-link hero-telegram" href={telegramBotUrl} target="_blank" rel="noreferrer">
                  {t(lang, 'openTelegram')}
                </a>
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
                    onClick={() => openImageUpload('ocr')}
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
                  onClick={() => openImageUpload('ocr')}
                >
                  {t(lang, 'ocr')}
                </button>
                <button
                  className="pill"
                  type="button"
                  onClick={() => openImageUpload('promptFromImage')}
                >
                  {t(lang, 'promptFromImage')}
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
                <button className="pill" type="button" onClick={() => void openCabinet()}>
                  {t(lang, 'cabinet')}
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
                      onClick={() => openImageUpload('promptFromImage')}
                    >
                      <strong>{t(lang, 'promptFromImage')}</strong>
                      <span>
                        {lang === 'ru'
                          ? 'Составить промпт по загруженному изображению'
                          : 'Create a text-to-image prompt from a photo'}
                      </span>
                    </button>
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
          if (file) {
            if (uploadIntentRef.current === 'promptFromImage') {
              void onPromptFromImageFile(file)
            } else {
              void onOcrFile(file)
            }
          }
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
