import { ApiError } from '../api/client'

const baseUrl = import.meta.env.VITE_API_URL ?? ''

// photoUrl values from the API are paths; this makes them work when the API is on another host.
export const apiUrl = (path: string) => `${baseUrl}${path}`

const maxSide = 1600

// Phone photos are often 4000px and 5+ MB. Shrinking to 1600px JPEG keeps them sharp on any screen
// at a few hundred KB, so uploads are quick on cell signal and the database stays small.
export async function shrinkPhoto(file: File): Promise<Blob> {
  let bitmap: ImageBitmap
  try {
    // from-image applies the phone's rotation flag so photos aren't sideways.
    bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' })
  } catch {
    throw new Error("Couldn't read that image. Try a JPEG or PNG.")
  }

  const scale = Math.min(1, maxSide / Math.max(bitmap.width, bitmap.height))
  const canvas = document.createElement('canvas')
  canvas.width = Math.round(bitmap.width * scale)
  canvas.height = Math.round(bitmap.height * scale)
  const context = canvas.getContext('2d')!
  // JPEG has no transparency; paint white so transparent PNGs don't turn black.
  context.fillStyle = '#ffffff'
  context.fillRect(0, 0, canvas.width, canvas.height)
  context.drawImage(bitmap, 0, 0, canvas.width, canvas.height)
  bitmap.close()

  return new Promise((resolve, reject) =>
    canvas.toBlob((blob) => (blob ? resolve(blob) : reject(new Error("Couldn't prepare the photo."))), 'image/jpeg', 0.85),
  )
}

// The photo endpoint takes raw image bytes, which the generated JSON client doesn't send, so this uses fetch directly.
export async function uploadPhoto(itemId: number, photo: Blob) {
  const response = await fetch(apiUrl(`/api/inventory/${itemId}/photo`), {
    method: 'PUT',
    headers: { 'Content-Type': photo.type || 'image/jpeg' },
    body: photo,
    credentials: 'include',
  })
  const body = await response.json().catch(() => undefined)
  if (!response.ok) {
    const error = new ApiError(response.status, body)
    throw error.fieldErrors.Photo ? Object.assign(error, { message: error.fieldErrors.Photo.join(' ') }) : error
  }
}
