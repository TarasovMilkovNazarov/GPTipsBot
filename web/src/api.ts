export type Me = {
  id: number
  isGuest: boolean
  firstName: string
  lastName?: string | null
  stars: number
  free: { gpt: number; images: number; ocr: number; animations: number; summaries: number }
  model: { id: string; name: string }
}

export type GptModel = {
  id: string
  displayName: string
  starsCost: number
  allowFreeQuota: boolean
  emoji: string
}

export type Conversation = { id: number; title: string; updatedAt: string; pinned?: boolean }

export type ChatMessage = {
  id?: number
  role: 'user' | 'assistant' | 'system'
  text: string
  imageUrl?: string
}

export type ImagePreset = { id: string; title: string; prompt: string }

export type PaymentPackage = { stars: number; rub: string; rubPerStar: number }

export type PaymentPackages = {
  enabled: boolean
  rubPerStar: number
  minRub: number
  minStars: number
  packages: PaymentPackage[]
}

async function json<T>(res: Response): Promise<T> {
  if (!res.ok) {
    let message = res.statusText
    try {
      const body = await res.json()
      message = body.message || message
    } catch {
      /* ignore */
    }
    throw new Error(message)
  }
  return res.json() as Promise<T>
}

export const api = {
  ensureGuest: () =>
    fetch('/api/auth/guest', { method: 'POST', credentials: 'include' }).then((r) => json<Me>(r)),

  me: () => fetch('/api/me', { credentials: 'include' }).then((r) => json<Me>(r)),

  logout: () =>
    fetch('/api/auth/logout', { method: 'POST', credentials: 'include' }).then((r) => json<{ ok: boolean }>(r)),

  telegramLogin: (payload: Record<string, unknown>) =>
    fetch('/api/auth/telegram', {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    }).then((r) => json<Me>(r)),

  models: () =>
    fetch('/api/models', { credentials: 'include' }).then((r) =>
      json<{ preferred: string; models: GptModel[] }>(r),
    ),

  setModel: (modelId: string) =>
    fetch('/api/models/preferred', {
      method: 'PUT',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ modelId }),
    }).then((r) => json<{ preferred: string }>(r)),

  conversations: () =>
    fetch('/api/conversations', { credentials: 'include' }).then((r) => json<Conversation[]>(r)),

  conversation: (id: number) =>
    fetch(`/api/conversations/${id}`, { credentials: 'include' }).then((r) =>
      json<{ id: number; messages: { id: number; role: string; text: string }[] }>(r),
    ),

  renameConversation: (id: number, title: string) =>
    fetch(`/api/conversations/${id}`, {
      method: 'PATCH',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ title }),
    }).then((r) => json<Conversation>(r)),

  pinConversation: (id: number, pinned: boolean) =>
    fetch(`/api/conversations/${id}/pin`, {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ pinned }),
    }).then((r) => json<Conversation>(r)),

  deleteConversation: (id: number) =>
    fetch(`/api/conversations/${id}`, {
      method: 'DELETE',
      credentials: 'include',
    }).then((r) => json<{ ok: boolean }>(r)),

  publicConfig: () =>
    fetch('/api/config/public', { credentials: 'include' }).then((r) =>
      json<{ botUsername: string; telegramLoginEnabled: boolean; yookassaEnabled?: boolean }>(r),
    ),

  paymentPackages: () =>
    fetch('/api/payments/packages', { credentials: 'include' }).then((r) => json<PaymentPackages>(r)),

  createYooKassaPayment: (stars: number) =>
    fetch('/api/payments/yookassa', {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ stars }),
    }).then((r) =>
      json<{ invoiceId: number; confirmationUrl: string; stars: number; rub: string }>(r),
    ),

  syncPayment: (invoiceId: number) =>
    fetch(`/api/payments/${invoiceId}/sync`, {
      method: 'POST',
      credentials: 'include',
    }).then((r) =>
      json<{ status: string; credited: boolean; me: Me }>(r),
    ),

  presets: () =>
    fetch('/api/presets/images', { credentials: 'include' }).then((r) => json<ImagePreset[]>(r)),

  generateImage: (prompt: string, size?: string, quality?: string) =>
    fetch('/api/images/generate', {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ prompt, size, quality }),
    }).then((r) => json<{ mimeType: string; base64: string; starsCharged: number }>(r)),

  ocr: async (file: File) => {
    const form = new FormData()
    form.append('file', file)
    return fetch('/api/ocr', { method: 'POST', credentials: 'include', body: form }).then((r) =>
      json<{ text: string }>(r),
    )
  },

  stt: async (file: Blob) => {
    const form = new FormData()
    form.append('file', file, 'voice.webm')
    return fetch('/api/stt', { method: 'POST', credentials: 'include', body: form }).then((r) =>
      json<{ text: string }>(r),
    )
  },

  async streamChat(
    text: string,
    newConversation: boolean,
    contextId: number | null,
    onEvent: (type: string, payload: Record<string, unknown>) => void,
  ) {
    const res = await fetch('/api/chat', {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ text, newConversation, contextId }),
    })
    if (!res.ok || !res.body) {
      throw new Error('Chat request failed')
    }
    const reader = res.body.getReader()
    const decoder = new TextDecoder()
    let buffer = ''
    while (true) {
      const { done, value } = await reader.read()
      if (done) break
      buffer += decoder.decode(value, { stream: true })
      const parts = buffer.split('\n\n')
      buffer = parts.pop() || ''
      for (const part of parts) {
        const line = part.trim()
        if (!line.startsWith('data:')) continue
        try {
          const data = JSON.parse(line.slice(5).trim()) as {
            type: string
            payload: Record<string, unknown>
          }
          onEvent(data.type, data.payload || {})
        } catch {
          /* ignore partial */
        }
      }
    }
  },
}
