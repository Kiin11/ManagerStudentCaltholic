/**
 * FEAT-P5-01: Cổng tra cứu thiếu nhi nội bộ tại HomePage (Đã đăng nhập)
 * - Tra cứu tức thời theo Tên, Tên Thánh, Mã hiện hành hoặc Mã năm cũ
 * - Hiển thị 360 độ: Chuyên cần, Chiến dịch đi lễ 1 tháng, ĐTB 2 năm trước, Bí tích, Liên hệ phụ huynh
 * - Client Cache + Server Redis Cache (TTL 5 phút) chuyển Option tức thời 0ms
 */

const student360ClientCache = new Map();
let currentLoadedStudent = null;
let searchDebounceTimer = null;

document.addEventListener('DOMContentLoaded', function () {
    const searchInput = document.getElementById('staffStudentQuickSearchInput');
    const clearBtn = document.getElementById('btnClearStaffSearch');
    const dropdown = document.getElementById('staffSearchDropdownMenu');
    const spinner = document.getElementById('staffSearchSpinner');

    if (!searchInput) return;

    // 1. Tìm kiếm tức thời kèm Debounce 280ms
    searchInput.addEventListener('input', function () {
        const term = this.value.trim();
        clearTimeout(searchDebounceTimer);

        if (term.length === 0) {
            if (clearBtn) clearBtn.classList.add('d-none');
            if (dropdown) dropdown.classList.add('d-none');
            if (spinner) spinner.classList.add('d-none');
            return;
        }

        if (clearBtn) clearBtn.classList.remove('d-none');
        if (spinner) spinner.classList.remove('d-none');

        searchDebounceTimer = setTimeout(async () => {
            try {
                const res = await fetch(`/Home/StaffSearchSuggestions?term=${encodeURIComponent(term)}`);
                const json = await res.json();
                if (spinner) spinner.classList.add('d-none');

                if (json.success && json.data) {
                    renderSuggestionsDropdown(json.data, term);
                }
            } catch (err) {
                if (spinner) spinner.classList.add('d-none');
                console.error('Lỗi tìm kiếm gợi ý:', err);
            }
        }, 280);
    });

    // 2. Nút Xóa ô tìm kiếm
    if (clearBtn) {
        clearBtn.addEventListener('click', function () {
            searchInput.value = '';
            this.classList.add('d-none');
            if (dropdown) dropdown.classList.add('d-none');
            searchInput.focus();
        });
    }

    // 3. Đóng dropdown khi click bên ngoài ô input
    document.addEventListener('click', function (e) {
        if (!searchInput.contains(e.target) && (!dropdown || !dropdown.contains(e.target))) {
            if (dropdown) dropdown.classList.add('d-none');
        }
    });
});

function renderSuggestionsDropdown(items, term) {
    const dropdown = document.getElementById('staffSearchDropdownMenu');
    const list = document.getElementById('staffSuggestionsList');
    if (!dropdown || !list) return;

    list.innerHTML = '';
    if (!items || items.length === 0) {
        list.innerHTML = `
            <div class="p-3 text-center text-muted small">
                <i class="bi bi-person-x fs-4 d-block mb-1 text-secondary"></i>
                Không tìm thấy thiếu nhi nào khớp với từ khóa "<strong>${escapeHtml(term)}</strong>".
            </div>`;
        dropdown.classList.remove('d-none');
        return;
    }

    items.forEach(item => {
        const btn = document.createElement('div');
        btn.className = 'list-group-item list-group-item-action p-2 px-3 suggestion-item border-bottom';

        let historicalBadge = '';
        if (item.isHistoricalMatch && item.matchedHistoricalCode) {
            historicalBadge = `<span class="badge bg-warning-subtle text-dark border ms-2" style="font-size: 0.72rem;">
                <i class="bi bi-clock-history me-1"></i>Mã năm cũ: ${escapeHtml(item.matchedHistoricalCode)}
            </span>`;
        }

        const initial = (item.firstName || item.fullName || 'T').substring(0, 1).toUpperCase();
        btn.innerHTML = `
            <div class="d-flex align-items-center justify-content-between gap-2">
                <div class="d-flex align-items-center gap-2">
                    <div class="rounded-circle bg-primary bg-opacity-10 text-primary d-flex align-items-center justify-content-center fw-bold"
                         style="width: 36px; height: 36px; font-size: 0.9rem;">
                        ${initial}
                    </div>
                    <div>
                        <div class="fw-bold text-dark d-flex align-items-center">
                            <span>${escapeHtml(item.christianName)} ${escapeHtml(item.fullName)}</span>
                            ${historicalBadge}
                        </div>
                        <div class="text-muted small">
                            <span class="badge bg-light text-secondary border me-1">${escapeHtml(item.studentCode)}</span>
                            <span>${escapeHtml(item.currentClassName || 'Chưa xếp lớp')}</span>
                            <span class="ms-1 text-muted">(${escapeHtml(item.currentAcademicYearName || '2026-2027')})</span>
                        </div>
                    </div>
                </div>
                <i class="bi bi-chevron-right text-muted small"></i>
            </div>
        `;

        btn.onclick = () => {
            dropdown.classList.add('d-none');
            fetchStudent360Detail(item.studentId, 'OVERVIEW', item.fullName);
        };
        list.appendChild(btn);
    });

    dropdown.classList.remove('d-none');
}

async function fetchStudent360Detail(studentId, option = 'OVERVIEW', searchTerm = '') {
    const wrapper = document.getElementById('staffLookupResultWrapper');
    const spinner = document.getElementById('staffSearchSpinner');

    // 1. Kiểm tra cache phía Client (0ms latency tức thì)
    if (student360ClientCache.has(studentId)) {
        currentLoadedStudent = student360ClientCache.get(studentId);
        renderStudent360ToDom(currentLoadedStudent, option);
        if (wrapper) {
            wrapper.classList.remove('d-none');
            wrapper.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
        }
        return;
    }

    if (spinner) spinner.classList.remove('d-none');

    try {
        // 2. Gọi API với Redis Cache TTL 5 phút phía Server
        const res = await fetch(`/Home/StaffStudentLookup?studentId=${studentId}&option=${encodeURIComponent(option)}&searchTerm=${encodeURIComponent(searchTerm)}`);
        const json = await res.json();
        if (spinner) spinner.classList.add('d-none');

        if (json.success && json.data) {
            student360ClientCache.set(studentId, json.data);
            currentLoadedStudent = json.data;
            renderStudent360ToDom(currentLoadedStudent, option);

            // Thêm vào chip Đã xem gần đây trên giao diện
            prependRecentLookupChip(json.data.basicInfo);

            if (wrapper) {
                wrapper.classList.remove('d-none');
                wrapper.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
            }
        } else {
            alert(json.message || 'Không thể tải thông tin thiếu nhi.');
        }
    } catch (err) {
        if (spinner) spinner.classList.add('d-none');
        console.error('Lỗi khi tra cứu 360 độ học sinh:', err);
        alert('Đã xảy ra lỗi khi kết nối tới máy chủ.');
    }
}

function switchLookupOptionTab(option) {
    // Cập nhật trạng thái nút Segmented
    document.querySelectorAll('.staff-option-btn').forEach(btn => {
        const opt = btn.getAttribute('data-option');
        if (opt === option) {
            btn.classList.add('active', 'btn-primary');
            btn.classList.remove('btn-outline-primary', 'btn-outline-success', 'btn-outline-warning', 'btn-outline-info');
        } else {
            btn.classList.remove('active', 'btn-primary');
            if (opt === 'OVERVIEW') btn.classList.add('btn-outline-primary');
            if (opt === 'ATTENDANCE_DETAIL') btn.classList.add('btn-outline-success');
            if (opt === 'ACADEMIC_2Y_SACRAMENT') btn.classList.add('btn-outline-warning');
            if (opt === 'PARENT_CONTACT') btn.classList.add('btn-outline-info');
        }
    });

    // Ẩn tất cả các panel và bật panel tương ứng
    document.querySelectorAll('.staff-option-panel').forEach(p => p.classList.add('d-none'));

    if (option === 'OVERVIEW') {
        const p = document.getElementById('panelOptionOverview');
        if (p) p.classList.remove('d-none');
    } else if (option === 'ATTENDANCE_DETAIL') {
        const p = document.getElementById('panelOptionAttendance');
        if (p) p.classList.remove('d-none');
    } else if (option === 'ACADEMIC_2Y_SACRAMENT') {
        const p = document.getElementById('panelOptionAcademic');
        if (p) p.classList.remove('d-none');
    } else if (option === 'PARENT_CONTACT') {
        const p = document.getElementById('panelOptionParent');
        if (p) p.classList.remove('d-none');
    }
}

function renderStudent360ToDom(data, selectedOption = 'OVERVIEW') {
    if (!data || !data.basicInfo) return;

    const b = data.basicInfo;
    const att = data.attendance || {};
    const acad = data.academic || {};
    const sac = data.sacrament || {};
    const par = data.parentContact || {};

    // 1. Header Tóm tắt
    const initial = (b.firstName || b.fullName || 'T').substring(0, 1).toUpperCase();
    const avatar = document.getElementById('resAvatarCircle');
    if (avatar) avatar.innerText = initial;

    document.getElementById('resStudentFullName').innerText = `${b.christianName} ${b.firstName} ${b.lastName}`.trim();
    document.getElementById('resStudentCodeBadge').innerText = b.studentCode;
    document.getElementById('resActiveClassBadge').innerText = b.currentClassName ? `Lớp: ${b.currentClassName}` : 'Chưa xếp lớp';
    document.getElementById('resAcademicYearName').innerText = b.currentAcademicYearName || '2026-2027';
    document.getElementById('resGender').innerText = b.gender || 'Nam';

    const dobDate = b.dateOfBirth ? new Date(b.dateOfBirth) : null;
    document.getElementById('resDob').innerText = dobDate ? dobDate.toLocaleDateString('vi-VN') : 'Chưa rõ';
    document.getElementById('resAge').innerText = b.age || '0';
    document.getElementById('resParentPhone').innerText = b.parentPhone || 'Chưa cập nhật';

    // 2. PANEL 1: OVERVIEW
    const sun = att.latestSunday;
    const resSunOver = document.getElementById('resSundayStatusOverview');
    if (resSunOver) {
        if (sun) {
            resSunOver.innerHTML = `<span class="badge bg-${sun.statusBadgeColor} me-1">${escapeHtml(sun.statusDisplay)}</span>
                <small class="text-muted">(${formatDateShort(sun.attendanceDate)})</small>`;
        } else {
            resSunOver.innerHTML = `<span class="badge bg-secondary">Chưa có dữ liệu</span>`;
        }
    }

    const thu = att.latestThursday;
    const resThuOver = document.getElementById('resThursdayStatusOverview');
    if (resThuOver) {
        if (thu) {
            resThuOver.innerHTML = `<span class="badge bg-${thu.statusBadgeColor} me-1">${escapeHtml(thu.statusDisplay)}</span>
                <small class="text-muted">(${formatDateShort(thu.attendanceDate)})</small>`;
        } else {
            resThuOver.innerHTML = `<span class="badge bg-secondary">Chưa có dữ liệu</span>`;
        }
    }

    const massRate = att.massAttendanceRatePercent || 0;
    const massRateTxt = document.getElementById('resMassRateText');
    const massRateBar = document.getElementById('resMassRateBar');
    if (massRateTxt) massRateTxt.innerText = `${massRate}%`;
    if (massRateBar) massRateBar.style.width = `${massRate}%`;

    const curAvg = acad.currentYearAverage != null ? acad.currentYearAverage.toFixed(2) : '--';
    document.getElementById('resCurrentAvgScore').innerText = curAvg;
    document.getElementById('resCurrentClassification').innerText = acad.currentClassification || 'Chưa xếp loại';
    document.getElementById('resClassRoomName').innerText = b.currentRoomName || 'Chưa xếp phòng';
    document.getElementById('resClassShift').innerText = b.currentShift || 'Sáng';

    const sacCount = sac.completedCount || 0;
    document.getElementById('resSacramentCompletedCount').innerText = `${sacCount}/4 mốc`;
    const sacBar = document.getElementById('resSacramentProgressBar');
    if (sacBar) sacBar.style.width = `${(sacCount / 4) * 100}%`;

    const sacQuickList = document.getElementById('resSacramentQuickList');
    if (sacQuickList) {
        sacQuickList.innerHTML = `
            <div class="d-flex justify-content-between align-items-center">
                <span>1. Rửa Tội:</span>
                ${renderSacBadge(sac.baptism)}
            </div>
            <div class="d-flex justify-content-between align-items-center">
                <span>2. Rước Lễ Lần Đầu:</span>
                ${renderSacBadge(sac.firstCommunion)}
            </div>
            <div class="d-flex justify-content-between align-items-center">
                <span>3. Thêm Sức:</span>
                ${renderSacBadge(sac.confirmation)}
            </div>
            <div class="d-flex justify-content-between align-items-center">
                <span>4. Bao Đồng / Sống Đạo:</span>
                ${renderSacBadge(sac.solemnProfession)}
            </div>
        `;
    }

    // 3. PANEL 2: CHUYÊN CẦN & CHIẾN DỊCH
    renderAttendancePanel(att);

    // 4. PANEL 3: HỌC VỤ 2 NĂM & BÍ TÍCH
    renderAcademicPanel(acad, sac);

    // 5. PANEL 4: LIÊN HỆ PHỤ HUYNH
    renderParentContactPanel(par, b);

    // Chuyển sang option mong muốn
    switchLookupOptionTab(selectedOption);
}

function renderAttendancePanel(att) {
    // Lễ Chúa Nhật
    const sunBadge = document.getElementById('resSundayBadge');
    const sunDate = document.getElementById('resSundayDate');
    const sunTime = document.getElementById('resSundayTime');
    if (att.latestSunday) {
        sunBadge.className = `badge bg-${att.latestSunday.statusBadgeColor}`;
        sunBadge.innerText = att.latestSunday.statusDisplay;
        sunDate.innerText = formatDateLong(att.latestSunday.attendanceDate);
        sunTime.innerText = att.latestSunday.checkInTimeDisplay || 'Chưa ghi nhận giờ';
    } else {
        sunBadge.className = 'badge bg-secondary';
        sunBadge.innerText = 'Chưa có dữ liệu';
        sunDate.innerText = '--/--/----';
        sunTime.innerText = '--:--';
    }

    // Lễ Thứ Năm
    const thuBadge = document.getElementById('resThursdayBadge');
    const thuDate = document.getElementById('resThursdayDate');
    const thuTime = document.getElementById('resThursdayTime');
    if (att.latestThursday) {
        thuBadge.className = `badge bg-${att.latestThursday.statusBadgeColor}`;
        thuBadge.innerText = att.latestThursday.statusDisplay;
        thuDate.innerText = formatDateLong(att.latestThursday.attendanceDate);
        thuTime.innerText = att.latestThursday.checkInTimeDisplay || 'Chưa ghi nhận giờ';
    } else {
        thuBadge.className = 'badge bg-secondary';
        thuBadge.innerText = 'Chưa có dữ liệu';
        thuDate.innerText = '--/--/----';
        thuTime.innerText = '--:--';
    }

    // Giờ học Giáo lý
    const classBadge = document.getElementById('resClassSessionBadge');
    const classDate = document.getElementById('resClassSessionDate');
    const classTime = document.getElementById('resClassSessionTime');
    if (att.latestClassSession) {
        classBadge.className = `badge bg-${att.latestClassSession.statusBadgeColor}`;
        classBadge.innerText = att.latestClassSession.statusDisplay;
        classDate.innerText = formatDateLong(att.latestClassSession.attendanceDate);
        classTime.innerText = att.latestClassSession.checkInTimeDisplay || 'Chưa ghi nhận giờ';
    } else {
        classBadge.className = 'badge bg-secondary';
        classBadge.innerText = 'Chưa có dữ liệu';
        classDate.innerText = '--/--/----';
        classTime.innerText = '--:--';
    }

    // Chiến dịch Đi Lễ 1 Tháng
    const camp = att.activeCampaign;
    const streakBadge = document.getElementById('resCampaignStreakBadge');
    const campTitle = document.getElementById('resCampaignTitle');
    const campPeriod = document.getElementById('resCampaignPeriod');
    const campAttended = document.getElementById('resCampaignAttendedText');
    const campRate = document.getElementById('resCampaignRatePercent');
    const campBar = document.getElementById('resCampaignProgressBar');
    const campDatesList = document.getElementById('resCampaignDatesList');

    if (camp) {
        streakBadge.innerText = `🔥 Chuỗi liên tục: ${camp.currentStreak} ngày`;
        campTitle.innerText = camp.title;
        campPeriod.innerText = `Thời gian: ${formatDateShort(camp.startDate)} đến ${formatDateShort(camp.endDate)}`;
        campAttended.innerText = `${camp.attendedDays}/${camp.targetDays} buổi ${camp.isTargetReached ? '(ĐẠT)' : ''}`;
        campRate.innerText = `${camp.completionRate}%`;
        campBar.style.width = `${camp.completionRate}%`;

        if (camp.recentPresentDatesIso && camp.recentPresentDatesIso.length > 0) {
            campDatesList.innerHTML = camp.recentPresentDatesIso.map(d => `
                <span class="badge bg-success-subtle text-success border"><i class="bi bi-check2 me-1"></i>${d}</span>
            `).join('');
        } else {
            campDatesList.innerHTML = `<span class="badge bg-light text-muted border">Chưa có ngày tham dự</span>`;
        }
    } else {
        streakBadge.innerText = 'Không có chiến dịch';
        campTitle.innerText = 'Hiện tại không có Chiến dịch đi lễ cho lớp này';
        campPeriod.innerText = 'Chỉ áp dụng cho các lớp chuẩn bị Bí tích / Tuyên hứa (RL2, TS3, BĐ3, DB4)';
        campAttended.innerText = '0/0 buổi';
        campRate.innerText = '0%';
        campBar.style.width = '0%';
        campDatesList.innerHTML = `<span class="badge bg-light text-muted border">Không áp dụng</span>`;
    }

    // Tổng quan năm
    const totMass = (att.totalMassAttendedCurrentYear || 0) + (att.totalMassMissedCurrentYear || 0);
    document.getElementById('resTotalMassStat').innerText =
        `${att.totalMassAttendedCurrentYear || 0} / ${totMass} buổi (${att.massAttendanceRatePercent || 0}%)`;

    const totClass = (att.totalClassAttendedCurrentYear || 0) + (att.totalClassMissedCurrentYear || 0);
    document.getElementById('resTotalClassStat').innerText =
        `${att.totalClassAttendedCurrentYear || 0} / ${totClass} buổi (${att.classAttendanceRatePercent || 0}%)`;
}

function renderAcademicPanel(acad, sac) {
    // 1. Bảng điểm năm hiện tại
    const tbodyCur = document.getElementById('resCurrentGradesTableBody');
    const gradeCount = document.getElementById('resCurGradeCount');
    if (acad.currentGrades && acad.currentGrades.length > 0) {
        gradeCount.innerText = `${acad.currentGrades.length} cột điểm`;
        tbodyCur.innerHTML = acad.currentGrades.map(g => `
            <tr>
                <td class="fw-semibold text-dark">${escapeHtml(g.columnName)}</td>
                <td class="text-center"><span class="badge bg-light text-dark border">x${g.weightFactor}</span></td>
                <td class="text-center fw-bold text-primary">${g.score != null ? g.score.toFixed(1) : '<span class="text-muted">--</span>'}</td>
                <td class="text-center"><span class="badge bg-secondary-subtle text-secondary">HK${g.semester}</span></td>
                <td class="text-muted small">${escapeHtml(g.note || '-')}</td>
            </tr>
        `).join('');
    } else {
        gradeCount.innerText = '0 cột điểm';
        tbodyCur.innerHTML = `<tr><td colspan="5" class="text-center py-3 text-muted">Chưa có cột điểm nào được nhập.</td></tr>`;
    }

    // 2. Lịch sử 2 Niên khóa trước đó
    const tbodyPast = document.getElementById('resPastTwoYearsTableBody');
    if (acad.pastTwoYears && acad.pastTwoYears.length > 0) {
        tbodyPast.innerHTML = acad.pastTwoYears.map(p => {
            const outcomeBadge = p.isPassed
                ? `<span class="badge bg-success-subtle text-success border"><i class="bi bi-check-circle me-1"></i>${escapeHtml(p.rolloverOutcomeDisplay)}</span>`
                : `<span class="badge bg-danger-subtle text-danger border"><i class="bi bi-x-circle me-1"></i>${escapeHtml(p.rolloverOutcomeDisplay)}</span>`;
            return `
                <tr>
                    <td class="fw-bold">${escapeHtml(p.academicYearName)}</td>
                    <td>${escapeHtml(p.className)}</td>
                    <td class="text-center fw-bold text-primary">${p.yearAverageScore != null ? p.yearAverageScore.toFixed(2) : '--'}</td>
                    <td class="text-center"><span class="badge bg-light text-dark border">${p.attendanceRatePercent}%</span></td>
                    <td class="text-center">${outcomeBadge}</td>
                </tr>
            `;
        }).join('');
    } else {
        tbodyPast.innerHTML = `<tr><td colspan="5" class="text-center py-3 text-muted">Chưa có dữ liệu lịch sử niên khóa trước.</td></tr>`;
    }

    // 3. 4 Mốc Bí tích
    const detailedCards = document.getElementById('resSacramentDetailedCards');
    if (detailedCards) {
        detailedCards.innerHTML = `
            <div class="col-12 col-md-6 col-xl-3">${buildSacramentCardHtml(sac.baptism, 'bi-water text-primary')}</div>
            <div class="col-12 col-md-6 col-xl-3">${buildSacramentCardHtml(sac.firstCommunion, 'bi-brightness-high-fill text-warning')}</div>
            <div class="col-12 col-md-6 col-xl-3">${buildSacramentCardHtml(sac.confirmation, 'bi-fire text-danger')}</div>
            <div class="col-12 col-md-6 col-xl-3">${buildSacramentCardHtml(sac.solemnProfession, 'bi-award-fill text-info')}</div>
        `;
    }
}

function buildSacramentCardHtml(item, iconClass) {
    if (!item) return '';
    const statusBadge = item.isReceived
        ? `<span class="badge bg-success"><i class="bi bi-check-circle me-1"></i>Đã lãnh nhận</span>`
        : `<span class="badge bg-secondary-subtle text-secondary border">Chưa lãnh nhận</span>`;

    const dateStr = item.receivedDate ? formatDateLong(item.receivedDate) : 'Chưa có ngày';
    return `
        <div class="p-3 rounded-4 border bg-light h-100 d-flex flex-column justify-content-between">
            <div>
                <div class="d-flex justify-content-between align-items-center mb-2">
                    <span class="fw-bold text-dark"><i class="bi ${iconClass} me-1 fs-5"></i>${escapeHtml(item.milestoneName)}</span>
                    ${statusBadge}
                </div>
                <div class="small text-muted mb-1">Ngày: <strong class="text-dark">${dateStr}</strong></div>
                <div class="small text-muted mb-1">Giáo xứ: <strong class="text-dark">${escapeHtml(item.parishName || 'GX Phú Trung')}</strong></div>
                ${item.priestName ? `<div class="small text-muted mb-1">Chủ tế: <strong class="text-dark">${escapeHtml(item.priestName)}</strong></div>` : ''}
                ${item.godparentName ? `<div class="small text-muted mb-1">Đỡ đầu: <strong class="text-dark">${escapeHtml(item.godparentName)}</strong></div>` : ''}
                ${item.registryBookNumber ? `<div class="small text-muted mb-0">Số sổ: <span class="badge bg-white text-dark border">${escapeHtml(item.registryBookNumber)}</span></div>` : ''}
            </div>
        </div>
    `;
}

function renderParentContactPanel(par, b) {
    const phoneLarge = document.getElementById('resParentPhoneLarge');
    const btnCall = document.getElementById('btnCallParent');
    const btnSms = document.getElementById('btnSmsParent');
    const btnZalo = document.getElementById('btnZaloParent');
    const address = document.getElementById('resParentAddress');

    const phone = par.parentPhone || b.parentPhone;
    if (phoneLarge) phoneLarge.innerText = par.formattedPhone || phone || 'Chưa cập nhật SĐT';

    if (phone) {
        const cleanPhone = phone.replace(/[^0-9]/g, '');
        if (btnCall) {
            btnCall.href = `tel:${cleanPhone}`;
            btnCall.classList.remove('disabled');
        }
        if (btnSms) {
            btnSms.href = `sms:${cleanPhone}`;
            btnSms.classList.remove('disabled');
        }
        if (btnZalo) {
            btnZalo.href = `https://zalo.me/${cleanPhone}`;
            btnZalo.classList.remove('disabled');
        }
    } else {
        if (btnCall) btnCall.classList.add('disabled');
        if (btnSms) btnSms.classList.add('disabled');
        if (btnZalo) btnZalo.classList.add('disabled');
    }

    if (address) address.innerText = par.address || 'Giáo xứ Phú Trung, Hạt Tân Sơn Nhì, TGP Sài Gòn';

    const tplContainer = document.getElementById('resParentTemplatesContainer');
    if (tplContainer && par.templates) {
        tplContainer.innerHTML = par.templates.map((t, idx) => `
            <div class="p-3 rounded-4 border bg-light">
                <div class="d-flex justify-content-between align-items-center mb-1">
                    <span class="fw-bold text-dark">${idx + 1}. ${escapeHtml(t.title)}</span>
                    <span class="badge bg-secondary-subtle text-secondary small">${t.category}</span>
                </div>
                <p class="small text-secondary mb-2 bg-white p-2 rounded-3 border" style="white-space: pre-line;">${escapeHtml(t.content)}</p>
                <div class="d-flex justify-content-end gap-2">
                    <button type="button" class="btn btn-outline-primary btn-sm rounded-pill px-3"
                            onclick="copyParentTemplateText('${escapeJs(t.content)}')">
                        <i class="bi bi-clipboard me-1"></i> Sao chép tin nhắn
                    </button>
                    ${phone ? `<a href="https://zalo.me/${phone.replace(/[^0-9]/g, '')}" target="_blank"
                                  class="btn btn-info text-white btn-sm rounded-pill px-3"
                                  onclick="copyParentTemplateText('${escapeJs(t.content)}')">
                        <i class="bi bi-chat-text-fill me-1"></i> Mở Zalo & Gửi
                    </a>` : ''}
                </div>
            </div>
        `).join('');
    }
}

function copyParentTemplateText(text) {
    navigator.clipboard.writeText(text).then(() => {
        alert('Đã sao chép nội dung tin nhắn vào bộ nhớ tạm! Bạn có thể dán (Paste) vào Zalo hoặc tin nhắn SMS gửi phụ huynh.');
    }).catch(() => {
        prompt('Vui lòng sao chép tin nhắn bên dưới:', text);
    });
}

function prependRecentLookupChip(basicInfo) {
    if (!basicInfo || !basicInfo.studentId) return;

    const chipsWrapper = document.getElementById('staffRecentChipsWrapper');
    const notice = document.getElementById('noRecentLookupsNotice');
    if (notice) notice.remove();

    if (!chipsWrapper) return;

    const existingBtn = chipsWrapper.querySelector(`button[onclick*="${basicInfo.studentId}"]`);
    if (existingBtn) {
        chipsWrapper.removeChild(existingBtn);
    }

    const newBtn = document.createElement('button');
    newBtn.type = 'button';
    newBtn.className = 'btn btn-sm btn-light border rounded-pill px-3 py-1 text-dark shadow-2xs hover-primary staff-recent-chip';
    newBtn.setAttribute('onclick', `fetchStudent360Detail(${basicInfo.studentId}, 'OVERVIEW', '${escapeJs(basicInfo.fullName)}')`);
    newBtn.innerHTML = `
        <span class="badge bg-secondary-subtle text-secondary me-1">${escapeHtml(basicInfo.studentCode)}</span>
        <strong>${escapeHtml(basicInfo.christianName)} ${escapeHtml(basicInfo.fullName)}</strong>
        ${basicInfo.currentClassName ? `<span class="text-muted small ms-1">(${escapeHtml(basicInfo.currentClassName)})</span>` : ''}
    `;

    chipsWrapper.insertBefore(newBtn, chipsWrapper.firstChild);
}

function closeStaffLookupResult() {
    const wrapper = document.getElementById('staffLookupResultWrapper');
    if (wrapper) wrapper.classList.add('d-none');
}

function renderSacBadge(milestone) {
    if (milestone && milestone.isReceived) {
        return `<span class="badge bg-success-subtle text-success border"><i class="bi bi-check-circle-fill me-1"></i>Đã nhận</span>`;
    }
    return `<span class="badge bg-light text-muted border">Chưa</span>`;
}

function formatDateShort(dateStr) {
    if (!dateStr) return '--/--';
    const d = new Date(dateStr);
    return `${d.getDate().toString().padStart(2, '0')}/${(d.getMonth() + 1).toString().padStart(2, '0')}`;
}

function formatDateLong(dateStr) {
    if (!dateStr) return '--/--/----';
    const d = new Date(dateStr);
    return `${d.getDate().toString().padStart(2, '0')}/${(d.getMonth() + 1).toString().padStart(2, '0')}/${d.getFullYear()}`;
}

function escapeHtml(str) {
    if (!str) return '';
    return String(str)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}

function escapeJs(str) {
    if (!str) return '';
    return String(str)
        .replace(/\\/g, '\\\\')
        .replace(/'/g, "\\'")
        .replace(/"/g, '\\"')
        .replace(/\n/g, '\\n')
        .replace(/\r/g, '');
}
