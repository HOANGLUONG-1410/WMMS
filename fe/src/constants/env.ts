const rawApiBaseUrl = (
  import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000/api/v1'
).replace(/\/+$/, '');

const derivedOrigin = rawApiBaseUrl.startsWith('http')
  ? rawApiBaseUrl.replace(/\/api\/v\d+$/i, '')
  : '';

export const env = {
  API_BASE_URL: rawApiBaseUrl,
  API_ORIGIN: (import.meta.env.VITE_API_ORIGIN ?? derivedOrigin).replace(/\/+$/, ''),
} as const;

/** Chuẩn hóa đường dẫn tệp tĩnh từ Backend (vd: /uploads/...) theo cấu hình .env */
export function resolveBackendAssetUrl(url?: string | null): string | null {
  if (!url) return null;
  const trimmed = url.trim();
  if (!trimmed) return null;

  if (
    trimmed.startsWith('data:') ||
    trimmed.startsWith('blob:') ||
    trimmed.startsWith('http://') ||
    trimmed.startsWith('https://')
  ) {
    return trimmed;
  }

  if (trimmed.startsWith('/') && env.API_ORIGIN) {
    return `${env.API_ORIGIN}${trimmed}`;
  }

  return trimmed;
}