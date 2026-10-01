const CACHE_NAME = 'parish-pwa-cache-v1';

// Danh mục tài nguyên cần cache để chạy offline
const STATIC_ASSETS = [
    '/',
    '/Attendance/Index',
    '/Attendance/ScanQr',
    '/manifest.json',
    'https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css',
    'https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.3/font/bootstrap-icons.min.css',
    'https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/js/bootstrap.bundle.min.js'
];

// 1. Cài đặt Service Worker và lưu sẵn các assets thiết yếu
self.addEventListener('install', (event) => {
    event.waitUntil(
        caches.open(CACHE_NAME).then((cache) => {
            console.log('[PWA SW] Pre-caching static assets...');
            return cache.addAll(STATIC_ASSETS);
        })
    );
    self.skipWaiting();
});

// 2. Kích hoạt và dọn dẹp các cache phiên bản cũ
self.addEventListener('activate', (event) => {
    event.waitUntil(
        caches.keys().then((keys) => {
            return Promise.all(
                keys.map((key) => {
                    if (key !== CACHE_NAME) {
                        console.log('[PWA SW] Xóa cache cũ:', key);
                        return caches.delete(key);
                    }
                })
            );
        })
    );
    self.clients.claim();
});

// 3. Xử lý bắt Fetch: Network First, fallback sang Cache khi mất sóng
self.addEventListener('fetch', (event) => {
    // Chỉ xử lý các yêu cầu GET, không can thiệp POST (như SaveBatch, ScanCheckIn)
    if (event.request.method !== 'GET') return;

    event.respondWith(
        fetch(event.request)
            .then((networkResponse) => {
                // Nếu lấy thành công từ mạng, cập nhật 1 bản sao vào cache
                if (networkResponse && networkResponse.status === 200) {
                    const responseClone = networkResponse.clone();
                    caches.open(CACHE_NAME).then((cache) => {
                        cache.put(event.request, responseClone);
                    });
                }
                return networkResponse;
            })
            .catch(async () => {
                // Khi mất mạng/mất sóng, phục hồi từ Cache
                const cachedResponse = await caches.match(event.request);
                if (cachedResponse) {
                    return cachedResponse;
                }
                // Nếu trang con chưa cache, đưa về trang chủ offline
                if (event.request.mode === 'navigate') {
                    return caches.match('/');
                }
            })
    );
});