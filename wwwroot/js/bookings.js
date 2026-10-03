(function () {
    const debounce = (fn, delay = 420) => {
        let timer;
        return (...args) => {
            clearTimeout(timer);
            timer = setTimeout(() => fn(...args), delay);
        };
    };

    document.querySelectorAll('[data-booking-filter]').forEach((input) => {
        const form = input.closest('form');
        input.addEventListener(input.tagName === 'INPUT' ? 'input' : 'change', debounce(() => form?.submit()));
    });

    document.querySelectorAll('[data-booking-counter]').forEach((el) => {
        if (matchMedia('(prefers-reduced-motion: reduce)').matches) return;
        const raw = el.textContent.trim();
        const value = Number(raw.replace(/,/g, ''));
        if (!Number.isFinite(value)) return;
        const start = performance.now();
        const fmt = new Intl.NumberFormat();
        const tick = (now) => {
            const p = Math.min((now - start) / 700, 1);
            el.textContent = fmt.format(Math.round(value * (1 - Math.pow(1 - p, 3))));
            if (p < 1) requestAnimationFrame(tick);
            else el.textContent = raw;
        };
        el.textContent = '0';
        requestAnimationFrame(tick);
    });

    const calendarEl = document.getElementById('bookingCalendar');
    if (calendarEl && window.FullCalendar) {
        const loading = document.querySelector('[data-calendar-loading]');
        const currency = calendarEl.dataset.currency || 'Rs';
        const events = JSON.parse(calendarEl.dataset.events || '[]').map(item => ({
            id: item.Id || item.id,
            title: item.Title || item.title,
            start: item.Start || item.start,
            end: item.End || item.end,
            backgroundColor: item.Color || item.color,
            borderColor: item.Color || item.color,
            extendedProps: item
        }));
        const calendar = new FullCalendar.Calendar(calendarEl, {
            initialView: 'timeGridWeek',
            height: '100%',
            contentHeight: 'auto',
            expandRows: true,
            nowIndicator: true,
            selectable: true,
            dayMaxEvents: 3,
            businessHours: {
                daysOfWeek: [0, 1, 2, 3, 4, 5, 6],
                startTime: '06:00',
                endTime: '23:59'
            },
            headerToolbar: { left: 'prev,next today', center: 'title', right: 'dayGridMonth,timeGridWeek,timeGridDay,listWeek' },
            buttonText: { today: 'Today', month: 'Month', week: 'Week', day: 'Day', list: 'List' },
            events,
            loading: isLoading => {
                if (loading) loading.classList.toggle('is-hidden', !isLoading);
            },
            eventDidMount: info => {
                info.el.setAttribute('title', info.event.title);
            },
            eventContent: info => {
                const props = normalizeEventProps(info.event.extendedProps);
                const bookingNumber = props.bookingNumber || `BKG-${String(info.event.id).padStart(6, '0')}`;
                const status = props.status || 'Pending';
                const statusClass = status.replace(/\s+/g, '').toLowerCase();
                const timeText = `${formatEventTime(info.event.start)} - ${formatEventTime(info.event.end)}`;
                const wrapper = document.createElement('div');
                wrapper.className = 'booking-calendar-event';
                wrapper.innerHTML = `
                    <div class="event-top"><strong>${escapeHtml(bookingNumber)}</strong><em class="event-status ${escapeHtml(statusClass)}">${escapeHtml(status)}</em></div>
                    <span>${escapeHtml(props.customer || info.event.title || 'Customer')}</span>
                    <small>${escapeHtml(props.court || 'Court')} • ${escapeHtml(timeText)}</small>
                `;
                return { domNodes: [wrapper] };
            },
            eventClick: info => {
                info.jsEvent.preventDefault();
                openQuickView(info.event, currency);
            },
            datesSet: () => {
                if (loading) loading.classList.add('is-hidden');
            }
        });
        calendar.render();
        loading?.classList.add('is-hidden');
        document.querySelector('[data-calendar-today]')?.addEventListener('click', () => calendar.today());
        document.querySelector('[data-calendar-refresh]')?.addEventListener('click', () => window.location.reload());
    }

    function normalizeEventProps(props) {
        return {
            bookingNumber: props.BookingNumber || props.bookingNumber,
            status: props.Status || props.status,
            customer: props.Customer || props.customer,
            court: props.Court || props.court,
            sport: props.Sport || props.sport,
            amount: props.Amount ?? props.amount,
            paymentStatus: props.PaymentStatus || props.paymentStatus || 'Open details'
        };
    }

    function formatEventTime(value) {
        if (!value) return '--:--';
        return new Intl.DateTimeFormat([], { hour: '2-digit', minute: '2-digit' }).format(value);
    }

    function escapeHtml(value) {
        return String(value ?? '').replace(/[&<>"']/g, char => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;' }[char]));
    }

    function openQuickView(event, currency) {
        const modal = document.querySelector('[data-booking-quick-modal]');
        if (!modal) return;
        const props = normalizeEventProps(event.extendedProps);
        const bookingNumber = props.bookingNumber || `BKG-${String(event.id).padStart(6, '0')}`;
        const timeText = `${formatEventTime(event.start)} - ${formatEventTime(event.end)}`;
        modal.querySelector('[data-quick-number]').textContent = bookingNumber;
        modal.querySelector('[data-quick-customer]').textContent = props.customer || event.title || '-';
        modal.querySelector('[data-quick-court]').textContent = props.court || '-';
        modal.querySelector('[data-quick-sport]').textContent = props.sport || '-';
        modal.querySelector('[data-quick-time]').textContent = timeText;
        modal.querySelector('[data-quick-status]').textContent = props.status || '-';
        modal.querySelector('[data-quick-payment]').textContent = props.paymentStatus || 'Open details';
        modal.querySelector('[data-quick-amount]').textContent = `${currency} ${Number(props.amount || 0).toLocaleString()}`;
        modal.querySelector('[data-quick-details]').href = `/Bookings/Details/${event.id}`;
        modal.querySelector('[data-quick-edit]').href = `/Bookings/Edit/${event.id}`;
        modal.hidden = false;
    }

    document.querySelectorAll('[data-booking-quick-close]').forEach(button => {
        button.addEventListener('click', () => {
            const modal = document.querySelector('[data-booking-quick-modal]');
            if (modal) modal.hidden = true;
        });
    });

    const modal = document.querySelector('[data-booking-modal]');
    let pendingButton = null;
    document.querySelectorAll('[data-booking-action]').forEach((button) => {
        button.addEventListener('click', () => {
            pendingButton = button;
            if (!modal) return;
            modal.hidden = false;
            modal.querySelector('[data-booking-modal-message]').textContent = button.dataset.message || 'Are you sure?';
            const reason = modal.querySelector('[data-booking-modal-reason]');
            reason.value = '';
            reason.style.display = button.dataset.reason === 'true' ? 'block' : 'none';
        });
    });
    document.querySelector('[data-booking-modal-close]')?.addEventListener('click', () => { if (modal) modal.hidden = true; });
    document.querySelector('[data-booking-modal-confirm]')?.addEventListener('click', async () => {
        if (!pendingButton) return;
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
        const reason = modal?.querySelector('[data-booking-modal-reason]')?.value || '';
        const body = new URLSearchParams();
        if (pendingButton.dataset.reason === 'true') body.set('reason', reason);
        try {
            const response = await fetch(pendingButton.dataset.url, { method: 'POST', headers: { 'RequestVerificationToken': token, 'Content-Type': 'application/x-www-form-urlencoded' }, body });
            const result = await response.json();
            window.GameHubToast?.show?.({ type: result.success ? 'success' : 'error', title: result.success ? 'Success' : 'Attention Required', message: result.message });
            if (result.success) setTimeout(() => location.reload(), 650);
        } catch {
            window.GameHubToast?.show?.({ type: 'error', title: 'Something Went Wrong', message: 'Action failed.' });
        } finally {
            if (modal) modal.hidden = true;
        }
    });

    document.querySelectorAll('[data-booking-form]').forEach((form) => {
        let dirty = false;
        form.addEventListener('input', () => { dirty = true; updateEndTime(form); updateLiveSummary(form); });
        form.addEventListener('change', () => { dirty = true; updateEndTime(form); updateLiveSummary(form); loadSlots(form); loadCustomer(form); loadFacilitiesOrCourts(form); calculatePrice(form); });
        form.addEventListener('submit', () => { dirty = false; form.querySelector('[type="submit"]')?.classList.add('is-loading'); });
        window.addEventListener('beforeunload', (event) => { if (dirty) { event.preventDefault(); event.returnValue = ''; } });
        updateEndTime(form); updateLiveSummary(form); loadSlots(form);
    });

    function field(form, selector) { return form.querySelector(selector); }
    function updateEndTime(form) {
        const start = field(form, '[data-start-time]')?.value;
        const duration = Number(field(form, '[data-duration]')?.value || 0);
        const end = field(form, '[data-end-time]');
        if (!start || !duration || !end) return;
        const [h, m] = start.split(':').map(Number);
        const date = new Date(2000, 0, 1, h, m + duration);
        end.value = `${String(date.getHours()).padStart(2, '0')}:${String(date.getMinutes()).padStart(2, '0')}`;
    }
    function updateLiveSummary(form) {
        const selected = sel => field(form, sel)?.selectedOptions?.[0]?.text || 'Not selected';
        const text = (sel, val) => { const target = document.querySelector(sel); if (target) target.textContent = val; };
        text('[data-summary-customer]', selected('[data-customer-select]'));
        text('[data-summary-sport]', selected('[data-sport-select]'));
        text('[data-summary-court]', selected('[data-court-select]'));
        text('[data-summary-date]', field(form, '[data-booking-date]')?.value || 'Not selected');
        text('[data-summary-time]', `${field(form, '[data-start-time]')?.value || '--:--'} - ${field(form, '[data-end-time]')?.value || '--:--'}`);
    }
    async function loadCustomer(form) {
        const id = field(form, '[data-customer-select]')?.value;
        const panel = document.querySelector('[data-customer-summary]');
        if (!id || !panel) return;
        const res = await fetch(`/Bookings/GetCustomerSummary?customerId=${encodeURIComponent(id)}`);
        const json = await res.json();
        if (!json.success) return;
        const c = json.customer;
        panel.innerHTML = `<strong>${escapeHtml(c.fullName)}</strong><span>${escapeHtml(c.customerCode)} • ${escapeHtml(c.primaryPhone)}</span><small>${c.membership ? `Member: ${escapeHtml(c.membership.plan)}` : 'No active membership'} ${c.isBlacklisted ? '• Blacklisted' : ''}</small>`;
    }
    async function loadFacilitiesOrCourts(form) {
        const sportId = field(form, '[data-sport-select]')?.value;
        const facilityId = field(form, '[data-facility-select]')?.value;
        const facility = field(form, '[data-facility-select]');
        const court = field(form, '[data-court-select]');
        if (sportId && facility && !facilityId) {
            const json = await (await fetch(`/Bookings/GetFacilitiesBySport?sportId=${sportId}`)).json();
            facility.innerHTML = '<option value="">Select facility</option>' + json.facilities.map(x => `<option value="${escapeHtml(x.id)}">${escapeHtml(x.name)}</option>`).join('');
        }
        if (sportId && facilityId && court) {
            const json = await (await fetch(`/Bookings/GetCourts?sportId=${sportId}&facilityId=${facilityId}`)).json();
            court.innerHTML = '<option value="">Select court</option>' + json.courts.map(x => `<option value="${escapeHtml(x.id)}">${escapeHtml(x.name)} (${escapeHtml(x.status)})</option>`).join('');
        }
    }
    async function loadSlots(form) {
        const courtId = field(form, '[data-court-select]')?.value || field(form, '[name="CourtId"]')?.value;
        const date = field(form, '[data-booking-date]')?.value;
        const duration = field(form, '[data-duration]')?.value;
        const grid = document.querySelector('[data-slot-grid]');
        if (!courtId || !date || !duration || !grid) return;
        const json = await (await fetch(`/Bookings/GetAvailableSlots?courtId=${courtId}&bookingDate=${date}&durationMinutes=${duration}&bookingId=${field(form, '[name="Id"]')?.value || ''}`)).json();
        grid.innerHTML = json.slots.length ? json.slots.map(slot => `<button type="button" class="booking-slot ${slot.isAvailable ? '' : 'unavailable'}" data-slot="${escapeHtml(slot.startTime)}" ${slot.isAvailable ? '' : 'disabled'}><strong>${escapeHtml(slot.displayText)}</strong><small>${slot.isPeakRate ? 'Peak' : 'Standard'} • ${escapeHtml(String(slot.basePrice))}</small></button>`).join('') : '<div class="booking-slot-empty">No slots available for this date.</div>';
        grid.querySelectorAll('[data-slot]').forEach(btn => btn.addEventListener('click', () => { field(form, '[data-start-time]').value = btn.dataset.slot; updateEndTime(form); updateLiveSummary(form); calculatePrice(form); grid.querySelectorAll('.selected').forEach(x => x.classList.remove('selected')); btn.classList.add('selected'); }));
    }
    async function calculatePrice(form) {
        const params = new URLSearchParams({
            customerId: field(form, '[data-customer-select]')?.value || 0,
            sportId: field(form, '[data-sport-select]')?.value || 0,
            courtId: field(form, '[data-court-select]')?.value || 0,
            bookingDate: field(form, '[data-booking-date]')?.value || '',
            startTime: field(form, '[data-start-time]')?.value || '',
            durationMinutes: field(form, '[data-duration]')?.value || 0,
            manualDiscountAmount: field(form, '[data-manual-discount]')?.value || 0,
            paidAmount: field(form, '[data-paid-amount]')?.value || 0,
            bookingId: field(form, '[name="Id"]')?.value || ''
        });
        if (!params.get('customerId') || !params.get('sportId') || !params.get('courtId') || !params.get('bookingDate') || !params.get('startTime')) return;
        const json = await (await fetch(`/Bookings/CalculatePrice?${params}`)).json();
        if (!json.summary) return;
        const s = json.summary;
        const panel = document.querySelector('[data-price-panel]');
        if (panel) panel.innerHTML = `<div><span>Base</span><strong>${escapeHtml(s.currencySymbol)} ${Number(s.baseAmount).toLocaleString()}</strong></div><div><span>Membership Discount</span><strong>${escapeHtml(s.currencySymbol)} ${Number(s.membershipDiscountAmount).toLocaleString()}</strong></div><div><span>Tax</span><strong>${escapeHtml(s.currencySymbol)} ${Number(s.taxAmount).toLocaleString()}</strong></div><div><span>Total</span><strong>${escapeHtml(s.currencySymbol)} ${Number(s.totalAmount).toLocaleString()}</strong></div>`;
        const total = document.querySelector('[data-summary-total]');
        if (total) total.textContent = `${s.currencySymbol} ${Number(s.totalAmount).toLocaleString()}`;
    }
})();
