const CACHE_VERSION = 'parish-qlgl-pwa-v3.5.0';
const STATIC_CACHE = `${CACHE_VERSION}-static`;
const DYNAMIC_PAGES_CACHE = `${CACHE_VERSION}-pages`;

// Danh sách tài nguyên tĩnh cốt lõi (App Shell) cần tải trước khi cài đặt
const PRECACHE_ASSETS = [
    '/Offline',
    '/manifest.json',
    '/image/logo.jpg',
    '/icons/icon-192x192.png',
    '/icons/icon-512x512.png',
    '/lib/html5-qrcode/html5-qrcode.min.js',
    '/js/pwa-installer.js',
    'https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css',
    'https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.3/font/bootstrap-icons.min.css',
    'https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/js/bootstrap.bundle.min.js'
];

// 1. SỰ KIỆN INSTALL: Tải trước các file tĩnh vào bộ nhớ đệm
self.addEventListener('install', (event) => {
    event.waitUntil(
        caches.open(STATIC_CACHE)
            .then(async (cache) => {
                // Tải từng tài nguyên độc lập để 1 link CDN lỗi không làm hỏng toàn bộ quá trình cài đặt
                const cachePromises = PRECACHE_ASSETS.map(async (url) => {
                    try {
                        const req = new Request(url, { cache: 'reload' });
                        const res = await fetch(req);
                        if (res && (res.ok || res.type === 'opaque')) {
                            await cache.put(req, res);
                        }
                    } catch (err) {
                        console.warn('[SW] Bỏ qua precache tài nguyên:', url);
                    }
                });
                await Promise.all(cachePromises);
            })
            .then(() => self.skipWaiting())
    );
});

// 2. SỰ KIỆN ACTIVATE: Xóa các Cache cũ từ phiên bản trước
self.addEventListener('activate', (event) => {
    event.waitUntil(
        caches.keys()
            .then((keys) => {
                return Promise.all(
                    keys
                        .filter((key) => key !== STATIC_CACHE && key !== DYNAMIC_PAGES_CACHE)
                        .map((oldKey) => caches.delete(oldKey))
                );
            })
            .then(() => self.clients.claim())
    );
});

// Kiểm tra xem URL có phải là tài nguyên tĩnh (CSS, JS, Font, Ảnh, Icon) hay không
function isStaticAsset(url) {
    const pathname = url.pathname.toLowerCase();
    return (
        pathname.endsWith('.css') ||
        pathname.endsWith('.js') ||
        pathname.endsWith('.png') ||
        pathname.endsWith('.jpg') ||
        pathname.endsWith('.jpeg') ||
        pathname.endsWith('.svg') ||
        pathname.endsWith('.ico') ||
        pathname.endsWith('.woff') ||
        pathname.endsWith('.woff2') ||
        pathname.startsWith('/lib/') ||
        pathname.startsWith('/icons/') ||
        pathname.startsWith('/image/') ||
        url.hostname.includes('cdn.jsdelivr.net') ||
        url.hostname.includes('fonts.googleapis.com') ||
        url.hostname.includes('fonts.gstatic.com') ||
        url.hostname.includes('unpkg.com')
    );
}

// Kiểm tra các đường dẫn nhạy cảm không được lưu vào Cache
function shouldBypassCache(url) {
    const path = url.pathname.toLowerCase();
    return (
        path.startsWith('/account/login') ||
        path.startsWith('/account/logout') ||
        path.startsWith('/account/refreshtoken') ||
        path.startsWith('/admin/logs') ||
        path.includes('export')
    );
}

// 3. SỰ KIỆN FETCH: Điều phối chiến lược Caching thông minh
self.addEventListener('fetch', (event) => {
    const request = event.request;

    // Chỉ can thiệp vào các truy vấn GET qua giao thức http/https
    if (request.method !== 'GET') return;

    const url = new URL(request.url);
    if (!url.protocol.startsWith('http')) return;

    if (shouldBypassCache(url)) return;

    // CHIẾN LƯỢC A: Stale-While-Revalidate cho các tệp tĩnh (CSS, JS, Fonts, Icons, Ảnh)
    if (isStaticAsset(url)) {
        event.respondWith(
            caches.match(request).then((cachedResponse) => {
                const networkFetch = fetch(request)
                    .then((networkResponse) => {
                        if (networkResponse && (networkResponse.ok || networkResponse.type === 'opaque')) {
                            const clone = networkResponse.clone();
                            caches.open(STATIC_CACHE).then((cache) => cache.put(request, clone));
                        }
                        return networkResponse;
                    })
                    .catch(() => cachedResponse);

                return cachedResponse || networkFetch;
            })
        );
        return;
    }

    // CHIẾN LƯỢC B: Network-First kèm Cache & Offline Fallback cho các trang HTML điều hướng
    if (request.mode === 'navigate' || (request.headers.get('accept') || '').includes('text/html')) {
        event.respondWith(
            fetch(request)
                .then((networkResponse) => {
                    // Lưu bản sao trang hợp lệ vào DYNAMIC_PAGES_CACHE để GLV mở lại được khi mất sóng
                    if (networkResponse && networkResponse.ok && networkResponse.status === 200) {
                        const clone = networkResponse.clone();
                        caches.open(DYNAMIC_PAGES_CACHE).then((cache) => cache.put(request, clone));
                    }
                    return networkResponse;
                })
                .catch(async () => {
                    // 1. Thử lấy đúng URL (kèm query string như ?classId=...) trong bộ nhớ đệm
                    const exactMatch = await caches.match(request);
                    if (exactMatch) return exactMatch;

                    // 2. Thử lấy theo đường dẫn gốc không kèm tham số
                    const pathMatch = await caches.match(url.pathname);
                    if (pathMatch) return pathMatch;

                    // 3. Fallback về màn hình /Offline thân thiện
                    const offlineFallback = await caches.match('/Offline');
                    return offlineFallback || new Response(
                        '<!DOCTYPE html><html lang="vi"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Offline</title></head><body style="font-family:sans-serif;text-align:center;padding:40px;"><h3>Thiết bị đang mất kết nối mạng</h3><p>Vui lòng kiểm tra lại kết nối Wi-Fi / 4G và thử lại.</p><button onclick="location.reload()" style="padding:10px 20px;border-radius:8px;background:#1e40af;color:#fff;border:none;">Thử lại</button></body></html>',
                        { headers: { 'Content-Type': 'text/html; charset=utf-8' } }
                    );
                })
        );
    }
});

// 4. Lắng nghe thông điệp kích hoạt nhanh Service Worker mới từ Client
self.addEventListener('message', (event) => {
    if (event.data && event.data.type === 'SKIP_WAITING') {
        self.skipWaiting();
    }
});