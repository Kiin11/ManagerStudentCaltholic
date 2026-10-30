/**
 * FEAT-P3-06: Offline-First Attendance Engine (IndexedDB)
 * Quản lý Object Stores:
 *  - students_cache: Lưu danh sách thiếu nhi phục vụ quét QR & điểm danh khi mất mạng
 *  - pending_attendances: Hàng đợi lưu các lượt quét QR và sổ điểm danh chưa đồng bộ
 */
const ParishOfflineEngine = (function () {
    'use strict';

    const DB_NAME = 'ParishOfflineDB';
    const DB_VERSION = 1;
    const STORE_STUDENTS = 'students_cache';
    const STORE_PENDING = 'pending_attendances';

    let dbPromise = null;
    let isSyncing = false;

    // 1. Khởi tạo và mở kết nối IndexedDB
    function openDatabase() {
        if (dbPromise) return dbPromise;

        dbPromise = new Promise((resolve, reject) => {
            if (!('indexedDB' in window)) {
                reject(new Error('Trình duyệt không hỗ trợ IndexedDB.'));
                return;
            }

            const request = indexedDB.open(DB_NAME, DB_VERSION);

            request.onupgradeneeded = (event) => {
                const db = event.target.result;

                // Store 1: students_cache (Key: studentCode)
                if (!db.objectStoreNames.contains(STORE_STUDENTS)) {
                    const studentStore = db.createObjectStore(STORE_STUDENTS, { keyPath: 'studentCode' });
                    studentStore.createIndex('by_classId', 'classId', { unique: false });
                    studentStore.createIndex('by_enrollmentId', 'enrollmentId', { unique: true });
                }

                // Store 2: pending_attendances (Key: clientRecordId)
                if (!db.objectStoreNames.contains(STORE_PENDING)) {
                    const pendingStore = db.createObjectStore(STORE_PENDING, { keyPath: 'clientRecordId' });
                    pendingStore.createIndex('by_date', 'attendanceDate', { unique: false });
                    pendingStore.createIndex('by_classId', 'classId', { unique: false });
                }
            };

            request.onsuccess = (event) => resolve(event.target.result);
            request.onerror = (event) => reject(event.target.error);
        });

        return dbPromise;
    }

    function getCsrfToken() {
        return document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    }

    // 2. Tải trước danh sách học sinh từ Server vào students_cache (Pre-fetch trước Thánh lễ)
    async function prefetchStudentsCache(classId = null) {
        if (!navigator.onLine) return { success: false, count: 0 };

        try {
            const url = classId
                ? `/Attendance/GetOfflineStudentsCache?classId=${classId}`
                : '/Attendance/GetOfflineStudentsCache';

            const res = await fetch(url, { headers: { 'Accept': 'application/json' } });
            if (!res.ok) return { success: false, count: 0 };

            const data = await res.json();
            if (!data.success || !Array.isArray(data.students)) {
                return { success: false, count: 0 };
            }

            const db = await openDatabase();
            await new Promise((resolve, reject) => {
                const tx = db.transaction(STORE_STUDENTS, 'readwrite');
                const store = tx.objectStore(STORE_STUDENTS);

                data.students.forEach((stu) => {
                    store.put({
                        studentCode: (stu.studentCode || '').trim().toUpperCase(),
                        enrollmentId: stu.enrollmentId,
                        studentId: stu.studentId,
                        christianName: stu.christianName || '',
                        fullName: stu.fullName || '',
                        gender: stu.gender || 'Nam',
                        classId: stu.classId,
                        className: stu.className || '',
                        gradeLevel: stu.gradeLevel || '',
                        cachedAt: new Date().toISOString()
                    });
                });

                tx.oncomplete = () => resolve();
                tx.onerror = (e) => reject(e.target.error);
            });

            localStorage.setItem('parish_offline_last_cached', new Date().toLocaleTimeString('vi-VN'));
            await updateOfflineUiCounters();
            return { success: true, count: data.students.length };
        } catch (err) {
            console.warn('[OfflineEngine] Không thể tải trước students_cache:', err);
            return { success: false, count: 0 };
        }
    }

    // 3. Lưu danh sách học sinh trực tiếp từ bảng DOM hiện tại trên trang Attendance/Index
    async function cacheStudentsFromDomTable(classId, className, gradeLevel, rowsData) {
        if (!Array.isArray(rowsData) || rowsData.length === 0) return;
        try {
            const db = await openDatabase();
            await new Promise((resolve, reject) => {
                const tx = db.transaction(STORE_STUDENTS, 'readwrite');
                const store = tx.objectStore(STORE_STUDENTS);

                rowsData.forEach((stu) => {
                    if (!stu.studentCode) return;
                    store.put({
                        studentCode: stu.studentCode.trim().toUpperCase(),
                        enrollmentId: stu.enrollmentId,
                        studentId: stu.studentId,
                        christianName: stu.christianName || '',
                        fullName: stu.fullName || '',
                        gender: stu.gender || 'Nam',
                        classId: classId,
                        className: className,
                        gradeLevel: gradeLevel,
                        cachedAt: new Date().toISOString()
                    });
                });

                tx.oncomplete = () => resolve();
                tx.onerror = (e) => reject(e.target.error);
            });
            await updateOfflineUiCounters();
        } catch (err) {
            console.warn('[OfflineEngine] Lỗi cache từ bảng DOM:', err);
        }
    }

    // 4. Tìm kiếm thiếu nhi trong IndexedDB theo StudentCode (Dùng cho Quét QR Offline)
    async function getCachedStudentByCode(studentCode) {
        if (!studentCode) return null;
        const cleanCode = studentCode.trim().toUpperCase();
        const db = await openDatabase();

        return new Promise((resolve, reject) => {
            const tx = db.transaction(STORE_STUDENTS, 'readonly');
            const store = tx.objectStore(STORE_STUDENTS);
            const req = store.get(cleanCode);

            req.onsuccess = () => resolve(req.result || null);
            req.onerror = (e) => reject(e.target.error);
        });
    }

    // 5. Đếm tổng số học sinh đã cache và số bản ghi đang chờ đồng bộ
    async function getStorageStats() {
        try {
            const db = await openDatabase();
            const studentsCount = await new Promise((resolve) => {
                const tx = db.transaction(STORE_STUDENTS, 'readonly');
                const req = tx.objectStore(STORE_STUDENTS).count();
                req.onsuccess = () => resolve(req.result || 0);
                req.onerror = () => resolve(0);
            });

            const pendingCount = await new Promise((resolve) => {
                const tx = db.transaction(STORE_PENDING, 'readonly');
                const req = tx.objectStore(STORE_PENDING).count();
                req.onsuccess = () => resolve(req.result || 0);
                req.onerror = () => resolve(0);
            });

            return { studentsCount, pendingCount };
        } catch {
            return { studentsCount: 0, pendingCount: 0 };
        }
    }

    // 6. Đánh giá khung giờ điểm danh QR phía Client (Đồng bộ 100% với AttendanceTimeHelper.cs)
    function evaluateOfflineCheckInTime(scanDate, gradeLevel) {
        const day = scanDate.getDay(); // 0: Sunday, 4: Thursday
        const hours = scanDate.getHours();
        const minutes = scanDate.getMinutes();
        const seconds = scanDate.getSeconds();
        const totalMinutes = hours * 60 + minutes;
        const timeStr = `${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;

        // 1. Lễ Thứ Năm (17:30 - 19:30 -> 1050 - 1170 phút)
        if (day === 4 && totalMinutes >= 17 * 60 + 30 && totalMinutes <= 19 * 60 + 30) {
            return {
                isMass: true,
                isClass: false,
                status: 'PRESENT',
                activityTitle: 'THÁNH LỄ',
                checkInTimeStr: timeStr
            };
        }

        // 2. Lễ Chúa Nhật (06:20 - 08:20 -> 380 - 500 phút)
        if (day === 0 && totalMinutes >= 6 * 60 + 20 && totalMinutes <= 8 * 60 + 20) {
            let status = 'LATE';
            if (totalMinutes < 6 * 60 + 35) status = 'LINED_UP';
            else if (totalMinutes < 7 * 60) status = 'PRESENT';

            return {
                isMass: true,
                isClass: false,
                status: status,
                activityTitle: 'THÁNH LỄ',
                checkInTimeStr: timeStr
            };
        }

        // 3. Giờ học Giáo lý Chúa Nhật
        if (day === 0) {
            const isMorningGrade = gradeLevel === 'Khai Tâm' || gradeLevel === 'Rước Lễ' || gradeLevel === 'Thêm Sức';
            if (isMorningGrade && totalMinutes >= 8 * 60 + 55 && totalMinutes <= 11 * 60) {
                return {
                    isMass: false,
                    isClass: true,
                    status: totalMinutes < 9 * 60 + 30 ? 'PRESENT' : 'LATE',
                    activityTitle: 'GIỜ HỌC GIÁO LÝ',
                    checkInTimeStr: timeStr
                };
            }

            if (gradeLevel === 'Bao Đồng' && totalMinutes >= 14 * 60 + 30 && totalMinutes <= 17 * 60 + 10) {
                return {
                    isMass: false,
                    isClass: true,
                    status: totalMinutes < 15 * 60 + 15 ? 'PRESENT' : 'LATE',
                    activityTitle: 'GIỜ HỌC GIÁO LÝ',
                    checkInTimeStr: timeStr
                };
            }
        }

        // Mặc định ngoài khung giờ
        return {
            isMass: false,
            isClass: true,
            status: 'PRESENT',
            activityTitle: 'GIỜ HỌC GIÁO LÝ',
            checkInTimeStr: timeStr
        };
    }

    // 7. Xử lý Quét mã QR Offline và đẩy vào pending_attendances (TASK-P3-06.3)
    async function processOfflineQrScan(studentCode) {
        const cleanCode = (studentCode || '').trim().toUpperCase();
        const student = await getCachedStudentByCode(cleanCode);

        if (!student) {
            return {
                success: false,
                studentCode: cleanCode,
                message: `Không tìm thấy mã '${cleanCode}' trong bộ nhớ đệm Offline! Hãy bấm "Tải dữ liệu Offline" khi có mạng.`
            };
        }

        const now = new Date();
        const todayStr = now.toISOString().split('T')[0];
        const evalResult = evaluateOfflineCheckInTime(now, student.gradeLevel);
        const activityKey = evalResult.isMass ? 'MASS' : 'CLASS';
        const clientRecordId = `QR_${todayStr}_${student.enrollmentId}_${activityKey}`;

        const db = await openDatabase();
        let isDuplicate = false;

        await new Promise((resolve, reject) => {
            const tx = db.transaction(STORE_PENDING, 'readwrite');
            const store = tx.objectStore(STORE_PENDING);
            const getReq = store.get(clientRecordId);

            getReq.onsuccess = () => {
                if (getReq.result) {
                    isDuplicate = true;
                } else {
                    store.put({
                        clientRecordId: clientRecordId,
                        recordType: 'QR_SCAN',
                        enrollmentId: student.enrollmentId,
                        classId: student.classId,
                        studentCode: student.studentCode,
                        attendanceDate: todayStr,
                        attendedMass: evalResult.isMass,
                        massStatus: evalResult.isMass ? evalResult.status : 'ABSENT_UNPERMITTED',
                        massCheckInTimeStr: evalResult.isMass ? evalResult.checkInTimeStr : null,
                        classAttended: evalResult.isClass,
                        classStatus: evalResult.isClass ? evalResult.status : 'ABSENT_UNPERMITTED',
                        classCheckInTimeStr: evalResult.isClass ? evalResult.checkInTimeStr : null,
                        note: '[Quét QR Offline]',
                        clientTimestampUtc: now.toISOString()
                    });
                }
            };

            tx.oncomplete = () => resolve();
            tx.onerror = (e) => reject(e.target.error);
        });

        await updateOfflineUiCounters();

        return {
            success: true,
            isOfflineSaved: true,
            isDuplicate: isDuplicate,
            studentCode: student.studentCode,
            christianName: student.christianName,
            fullName: student.fullName,
            className: student.className,
            gradeLevel: student.gradeLevel,
            activityType: `${evalResult.activityTitle} (OFFLINE)`,
            status: evalResult.status,
            checkInTimeStr: evalResult.checkInTimeStr,
            message: isDuplicate
                ? `[Offline] Thiếu nhi đã được quét ${evalResult.activityTitle} trên máy này!`
                : `[Lưu Offline] Điểm danh thành công: ${student.fullName}`
        };
    }

    // 8. Lưu toàn bộ bảng điểm danh lớp vào IndexedDB khi mất mạng (TASK-P3-06.2)
    async function saveBatchAttendanceOffline(classId, attendanceDate, items) {
        if (!Array.isArray(items) || items.length === 0) {
            return { success: false, message: 'Không có dữ liệu học sinh để lưu.' };
        }

        const db = await openDatabase();
        const nowIso = new Date().toISOString();

        await new Promise((resolve, reject) => {
            const tx = db.transaction(STORE_PENDING, 'readwrite');
            const store = tx.objectStore(STORE_PENDING);

            items.forEach((item) => {
                const recordId = `BATCH_${attendanceDate}_${classId}_${item.enrollmentId}`;
                store.put({
                    clientRecordId: recordId,
                    recordType: 'BATCH_ROW',
                    enrollmentId: item.enrollmentId,
                    classId: classId,
                    studentCode: item.studentCode || '',
                    attendanceDate: attendanceDate,
                    attendedMass: item.attendedMass,
                    massStatus: item.massStatus,
                    massCheckInTimeStr: null,
                    classAttended: item.classAttended,
                    classStatus: item.classStatus,
                    classCheckInTimeStr: null,
                    note: item.note || '',
                    clientTimestampUtc: nowIso
                });
            });

            tx.oncomplete = () => resolve();
            tx.onerror = (e) => reject(e.target.error);
        });

        await updateOfflineUiCounters();

        return {
            success: true,
            isOffline: true,
            count: items.length,
            message: `Đang mất mạng: Đã lưu tạm ${items.length} học sinh vào bộ nhớ thiết bị (IndexedDB). Hệ thống sẽ tự động đồng bộ ngay khi có sóng!`
        };
    }

    // 9. Lấy toàn bộ danh sách bản ghi đang chờ đồng bộ
    async function getAllPendingRecords() {
        const db = await openDatabase();
        return new Promise((resolve, reject) => {
            const tx = db.transaction(STORE_PENDING, 'readonly');
            const store = tx.objectStore(STORE_PENDING);
            const req = store.getAll();

            req.onsuccess = () => resolve(req.result || []);
            req.onerror = (e) => reject(e.target.error);
        });
    }

    // 10. Xóa các bản ghi đã đồng bộ thành công khỏi pending_attendances
    async function removeSyncedRecords(clientRecordIds) {
        if (!Array.isArray(clientRecordIds) || clientRecordIds.length === 0) return;
        const db = await openDatabase();

        await new Promise((resolve, reject) => {
            const tx = db.transaction(STORE_PENDING, 'readwrite');
            const store = tx.objectStore(STORE_PENDING);
            clientRecordIds.forEach((id) => store.delete(id));
            tx.oncomplete = () => resolve();
            tx.onerror = (e) => reject(e.target.error);
        });
    }

    // 11. Đồng bộ tự động toàn bộ hàng đợi lên Server (TASK-P3-06.4)
    async function syncPendingAttendances(showFeedback = true) {
        if (isSyncing || !navigator.onLine) return;

        const pendingList = await getAllPendingRecords();
        if (pendingList.length === 0) {
            await updateOfflineUiCounters();
            return;
        }

        isSyncing = true;
        toggleSyncProgressBar(true, 25, `Đang đồng bộ ${pendingList.length} bản ghi điểm danh lên máy chủ...`);

        try {
            const res = await fetch('/Attendance/SyncOfflineQueue', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json',
                    'X-CSRF-TOKEN': getCsrfToken()
                },
                body: JSON.stringify({ records: pendingList })
            });

            toggleSyncProgressBar(true, 75, 'Đang xác nhận dữ liệu từ máy chủ...');

            if (res.ok) {
                const data = await res.json();
                if (data.success) {
                    const idsToRemove = data.syncedClientIds && data.syncedClientIds.length > 0
                        ? data.syncedClientIds
                        : pendingList.map(x => x.clientRecordId);

                    await removeSyncedRecords(idsToRemove);
                    toggleSyncProgressBar(true, 100, data.message);

                    if (showFeedback && typeof window.showAlert === 'function') {
                        window.showAlert(data.message, true);
                    }
                }
            }
        } catch (err) {
            console.warn('[OfflineEngine] Đồng bộ thất bại, sẽ thử lại sau:', err);
        } finally {
            isSyncing = false;
            setTimeout(() => toggleSyncProgressBar(false, 0, ''), 1800);
            await updateOfflineUiCounters();
        }
    }

    // 12. Bảo mật: Xóa sạch students_cache khi Giáo lý viên đăng xuất (TASK-P3-06.5)
    async function clearCacheOnLogout() {
        try {
            const db = await openDatabase();
            await new Promise((resolve) => {
                const tx = db.transaction([STORE_STUDENTS], 'readwrite');
                tx.objectStore(STORE_STUDENTS).clear();
                tx.oncomplete = () => resolve();
                tx.onerror = () => resolve();
            });
            localStorage.removeItem('parish_offline_last_cached');
        } catch (e) {
            console.warn('[OfflineEngine] Lỗi xóa cache khi đăng xuất:', e);
        }
    }

    // 13. Cập nhật giao diện thanh trạng thái Offline Mode & Bộ đếm
    async function updateOfflineUiCounters() {
        const stats = await getStorageStats();
        const isOnline = navigator.onLine;

        const modeBanner = document.getElementById('offlineAttendanceStatusBar');
        const badgePending = document.getElementById('badgeOfflinePendingCount');
        const badgeCached = document.getElementById('badgeOfflineCachedCount');
        const btnManualSync = document.getElementById('btnOfflineManualSync');

        if (badgePending) {
            badgePending.textContent = stats.pendingCount;
            badgePending.classList.toggle('bg-danger', stats.pendingCount > 0);
            badgePending.classList.toggle('bg-secondary', stats.pendingCount === 0);
        }

        if (badgeCached) {
            badgeCached.textContent = `${stats.studentsCount} thiếu nhi`;
        }

        if (btnManualSync) {
            btnManualSync.classList.toggle('d-none', stats.pendingCount === 0 || !isOnline);
        }

        if (modeBanner) {
            if (!isOnline) {
                modeBanner.className = 'alert alert-warning border-warning border-start border-4 shadow-sm rounded-3 py-2 px-3 mb-3 d-flex flex-wrap justify-content-between align-items-center';
            } else if (stats.pendingCount > 0) {
                modeBanner.className = 'alert alert-info border-info border-start border-4 shadow-sm rounded-3 py-2 px-3 mb-3 d-flex flex-wrap justify-content-between align-items-center';
            } else {
                modeBanner.className = 'alert alert-light border shadow-sm rounded-3 py-2 px-3 mb-3 d-flex flex-wrap justify-content-between align-items-center';
            }
        }
    }

    function toggleSyncProgressBar(show, percent, text) {
        const wrap = document.getElementById('offlineSyncProgressWrap');
        const bar = document.getElementById('offlineSyncProgressBar');
        const label = document.getElementById('offlineSyncProgressText');

        if (!wrap) return;
        if (show) {
            wrap.classList.remove('d-none');
            if (bar) bar.style.width = `${percent}%`;
            if (label) label.textContent = text || 'Đang đồng bộ...';
        } else {
            wrap.classList.add('d-none');
        }
    }

    // Tự động lắng nghe sự kiện Mạng Online / Offline
    window.addEventListener('online', () => {
        updateOfflineUiCounters();
        syncPendingAttendances(true);
    });

    window.addEventListener('offline', () => {
        updateOfflineUiCounters();
    });

    document.addEventListener('DOMContentLoaded', () => {
        updateOfflineUiCounters();
        if (navigator.onLine) {
            syncPendingAttendances(false);
        }
    });

    return {
        prefetchStudentsCache,
        cacheStudentsFromDomTable,
        getCachedStudentByCode,
        processOfflineQrScan,
        saveBatchAttendanceOffline,
        syncPendingAttendances,
        clearCacheOnLogout,
        updateOfflineUiCounters
    };
})();