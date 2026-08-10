import FingerprintJS from '@fingerprintjs/fingerprintjs'

const fpPromise = FingerprintJS.load({ monitoring: false })

/** Stable browser visitorId (FingerprintJS OSS). Swap to Pro agent later if needed. */
export async function getVisitorId(): Promise<string | null> {
  try {
    const fp = await fpPromise
    const result = await fp.get()
    const id = result.visitorId?.trim()
    return id && id.length >= 8 ? id : null
  } catch {
    return null
  }
}
