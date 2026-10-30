(function () {
    'use strict';

    let deferredInstallPrompt = null;
    const DISMISS_STORAGE_KEY = 'pwa_install_banner_dismissed_at';
    const DISMISS_COOLDOWN_HOURS = 48;

    // Kiểm tra ứng dụng đã được chạy ở chế độ cài đặt độc lập (Standalone) hay chưa
    function isRunningStandalone() {
        return (
            window.matchMedia('(display-mode: standalone)').matches ||
            window.navigator.standalone === true ||
            document.referrer.includes('android-app://')
        );
    }

    // Kiểm tra thiết bị có phải là iPhone / iPad / iPod hay không
    function isIosDevice() {
        const ua = window.navigator.userAgent.toLowerCase();
        const isClassicIos = /iphone|ipad|ipod/.test(ua);
        const isIpadOs = navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1;
        return isClassicIos || isIpadOs;
    }

    // Kiểm tra người dùng có vừa tắt banner gần đây không
    function isBannerRecentlyDismissed() {
        const raw = localStorage.getItem(DISMISS_STORAGE_KEY);
        if (!raw) return false;
        const dismissedTime = parseInt(raw, 10);
        if (isNaN(dismissedTime)) return false;
        const elapsedHours = (Date.now() - dismissedTime) / (1000 * 60 * 60);
        return elapsedHours < DISMISS_COOLDOWN_HOURS;
    }

    // Hiển thị các nút cài đặt trên Topbar, Sidebar và Banner nổi
    function toggleInstallButtons(visible) {
        const topbarBtn = document.getElementById('btnInstallPwaTopbar');
        const sidebarItem = document.getElementById('navItemInstallPwa');
        const floatingBanner = document.getElementById('pwaInstallBanner');

        if (topbarBtn) {
            topbarBtn.classList.toggle('d-none', !visible);
        }
        if (sidebarItem) {
            sidebarItem.classList.toggle('d-none', !visible);
        }
        if (floatingBanner) {
            if (visible && !isBannerRecentlyDismissed() && window.innerWidth < 992) {
                floatingBanner.classList.remove('d-none');
            } else {
                floatingBanner.classList.add('d-none');
            }
        }
    }

    // 1. Đăng ký Service Worker & Kiểm tra bản cập nhật mới
    function registerServiceWorker() {
        if (!('serviceWorker' in navigator)) return;

        window.addEventListener('load', async () => {
            try {
                const registration = await navigator.serviceWorker.register('/sw.js', { scope: '/' });
                console.log('[PWA] Service Worker đã kích hoạt tại scope:', registration.scope);

                registration.addEventListener('updatefound', () => {
                    const newWorker = registration.installing;
                    if (!newWorker) return;

                    newWorker.addEventListener('statechange', () => {
                        if (newWorker.state === 'installed' && navigator.serviceWorker.controller) {
                            showUpdateToast(newWorker);
                        }
                    });
                });
            } catch (err) {
                console.warn('[PWA] Không thể đăng ký Service Worker:', err);
            }
        });

        let refreshing = false;
        navigator.serviceWorker.addEventListener('controllerchange', () => {
            if (refreshing) return;
            refreshing = true;
            window.location.reload();
        });
    }

    function showUpdateToast(worker) {
        const toastEl = document.getElementById('pwaUpdateToast');
        const btnReload = document.getElementById('btnApplyPwaUpdate');
        if (!toastEl) return;

        toastEl.classList.remove('d-none');
        if (btnReload) {
            btnReload.onclick = function () {
                worker.postMessage({ type: 'SKIP_WAITING' });
            };
        }
    }

    // 2. Xử lý sự kiện cài đặt trên Android / Chrome / Edge / Desktop
    window.addEventListener('beforeinstallprompt', (e) => {
        e.preventDefault();
        deferredInstallPrompt = e;

        if (!isRunningStandalone()) {
            toggleInstallButtons(true);
        }
    });

    window.addEventListener('appinstalled', () => {
        deferredInstallPrompt = null;
        toggleInstallButtons(false);
        console.log('[PWA] Ứng dụng Quản Lý Giáo Lý đã được cài đặt thành công.');
    });

    // 3. Kích hoạt cài đặt khi người dùng bấm nút "Cài Đặt App"
    window.triggerPwaInstall = async function () {
        if (deferredInstallPrompt) {
            deferredInstallPrompt.prompt();
            const { outcome } = await deferredInstallPrompt.userChoice;
            console.log('[PWA] Kết quả lựa chọn cài đặt:', outcome);
            if (outcome === 'accepted') {
                deferredInstallPrompt = null;
                toggleInstallButtons(false);
            }
            return;
        }

        // Nếu là thiết bị iOS / iPadOS hoặc trình duyệt không hỗ trợ auto-prompt -> Mở Modal hướng dẫn
        const iosModalEl = document.getElementById('iosPwaInstallModal');
        if (iosModalEl && typeof bootstrap !== 'undefined') {
            bootstrap.Modal.getOrCreateInstance(iosModalEl).show();
        }
    };

    // 4. Tạm ẩn Banner cài đặt dưới đáy màn hình
    window.dismissPwaBanner = function () {
        localStorage.setItem(DISMISS_STORAGE_KEY, Date.now().toString());
        const floatingBanner = document.getElementById('pwaInstallBanner');
        if (floatingBanner) {
            floatingBanner.classList.add('d-none');
        }
    };

    // 5. Khởi tạo trạng thái ban đầu khi tải trang
    document.addEventListener('DOMContentLoaded', () => {
        if (isRunningStandalone()) {
            toggleInstallButtons(false);
            return;
        }

        // Nếu đang mở trên iPhone / iPad bằng Safari thì hiển thị nút hướng dẫn cài đặt
        if (isIosDevice()) {
            toggleInstallButtons(true);
        }
    });

    registerServiceWorker();
})();