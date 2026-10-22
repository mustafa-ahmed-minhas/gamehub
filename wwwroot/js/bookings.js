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
    let pendingForm = null;
    document.querySelectorAll('[data-booking-action]').forEach((element) => {
        element.addEventListener('click', (e) => {
            e.preventDefault();
            const form = element.closest('form');
            if (!form) return;
            pendingForm = form;
            if (!modal) return;
            modal.hidden = false;
            modal.querySelector('[data-booking-modal-message]').textContent = element.dataset.message || 'Are you sure?';
            const reason = modal.querySelector('[data-booking-modal-reason]');
            reason.value = '';
            reason.style.display = element.dataset.reason === 'true' ? 'block' : 'none';
            const charCount = reason.dataset.charCount;
            if (charCount) {
                const counter = modal.querySelector('[data-char-counter]');
                if (counter) counter.textContent = `${reason.value.length} / 10 characters minimum`;
            }
        });
    });
    document.querySelector('[data-booking-modal-close]')?.addEventListener('click', () => { if (modal) modal.hidden = true; pendingForm = null; });
    
    const reasonInput = modal?.querySelector('[data-booking-modal-reason]');
    if (reasonInput) {
        reasonInput.addEventListener('input', () => {
            const counter = modal?.querySelector('[data-char-counter]');
            if (counter) counter.textContent = `${reasonInput.value.length} / 10 characters minimum`;
        });
    }
    
    document.querySelector('[data-booking-modal-confirm]')?.addEventListener('click', async () => {
        if (!pendingForm) return;
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
        const reason = modal?.querySelector('[data-booking-modal-reason]')?.value || '';
        const requiresReason = pendingForm.dataset.reason === 'true';
        
        if (requiresReason && reason.trim().length < 10) {
            const counter = modal?.querySelector('[data-char-counter]');
            if (counter) {
                counter.textContent = 'Minimum 10 characters required';
                counter.style.color = '#9b2828';
            }
            reasonInput?.focus();
            return;
        }
        
        const formData = new FormData(pendingForm);
        if (requiresReason) formData.set('reason', reason.trim());
        
        try {
            const response = await fetch(pendingForm.action, { method: 'POST', headers: { 'RequestVerificationToken': token }, body: new URLSearchParams(formData) });
            const result = await response.json();
            window.GameHubToast?.show?.({ type: result.success ? 'success' : 'error', title: result.success ? 'Success' : 'Attention Required', message: result.message });
            if (result.success) setTimeout(() => location.reload(), 650);
        } catch {
            window.GameHubToast?.show?.({ type: 'error', title: 'Something Went Wrong', message: 'Action failed.' });
        } finally {
            if (modal) modal.hidden = true;
            pendingForm = null;
        }
    });

    document.querySelectorAll('[data-booking-form]').forEach((form) => {
        setupBookingForm(form);
    });

    /* ---------------------------------------------------------------------
       Manual booking form: customer autocomplete, court cascade, dynamic
       slots and live server priced totals.
       --------------------------------------------------------------------- */
    function setupBookingForm(form) {
        const state = { priceToken: 0, slotToken: 0, searchToken: 0, customerLabel: '', currency: form.dataset.currency || 'Rs' };
        const q = (selector) => form.querySelector(selector);
        const value = (selector) => q(selector)?.value?.trim() || '';

        const customerSearch = q('[data-customer-search]');
        const customerId = q('[data-customer-id]');
        const customerResults = q('[data-customer-results]');
        const customerClear = q('[data-customer-clear]');
        const customerSummary = q('[data-customer-summary]');
        const sportSelect = q('[data-sport-select]');
        const facilitySelect = q('[data-facility-select]');
        const courtSelect = q('[data-court-select]');
        const dateInput = q('[data-booking-date]');
        const slotGrid = q('[data-slot-grid]');
        const slotStatus = q('[data-slot-status]');
        const slotHint = q('[data-slot-hint]');
        const startInput = q('[data-start-time]');
        const endInput = q('[data-end-time]');
        const durationInput = q('[data-duration]');
        const discountInput = q('[data-manual-discount]');
        const paidInput = q('[data-paid-amount]');
        const statusSelect = q('[data-status-select]');
        const priceNote = q('[data-price-note]');

        const courtValue = () => courtSelect?.value?.trim() || q('[name="CourtId"]')?.value?.trim() || '';
        const bookingId = () => q('[name="Id"]')?.value?.trim() || '';
        const slotDurationVisible = durationInput && durationInput.type !== 'hidden';

        /* ---------------- customer autocomplete ---------------- */
        if (customerSearch && customerId && customerResults) {
            let timer = null;
            let items = [];
            let activeIndex = -1;

            const closeResults = () => {
                customerResults.classList.remove('open');
                customerResults.innerHTML = '';
                items = [];
                activeIndex = -1;
                customerSearch.setAttribute('aria-expanded', 'false');
            };

            const highlight = () => {
                customerResults.querySelectorAll('.customer-result').forEach((button, index) => {
                    button.classList.toggle('active', index === activeIndex);
                    if (index === activeIndex) button.scrollIntoView({ block: 'nearest' });
                });
            };

            const selectCustomer = (customer) => {
                customerId.value = customer.id;
                state.customerLabel = `${customer.name} (${customer.code})`;
                customerSearch.value = `${customer.name} — ${customer.phone}`;
                closeResults();
                clearFieldError(form, 'CustomerId');
                if (customerClear) customerClear.hidden = false;
                loadCustomerSummary(form, customerSummary, customer.id, state);
                updateLiveSummary(form, state);
                refreshPrice(form, state);
            };

            const renderResults = (list) => {
                items = list;
                activeIndex = -1;
                customerResults.classList.add('open');
                customerSearch.setAttribute('aria-expanded', 'true');
                if (!list.length) {
                    customerResults.innerHTML = '<div class="customer-empty">No matching customers found.</div>';
                    return;
                }
                customerResults.innerHTML = list.map((customer, index) => `
                    <button type="button" class="customer-result" role="option" data-index="${index}" data-id="${escapeHtml(customer.id)}" data-name="${escapeHtml(customer.name)}" data-phone="${escapeHtml(customer.phone)}" data-code="${escapeHtml(customer.code)}">
                        <strong>${escapeHtml(customer.name)}</strong>
                        <span>${escapeHtml(customer.code)} &bull; ${escapeHtml(customer.phone)}</span>
                    </button>`).join('');
                customerResults.querySelectorAll('.customer-result').forEach((button) => {
                    button.addEventListener('mousedown', (event) => event.preventDefault());
                    button.addEventListener('click', () => selectCustomer({
                        id: button.dataset.id,
                        name: button.dataset.name,
                        phone: button.dataset.phone,
                        code: button.dataset.code
                    }));
                });
            };

            customerSearch.addEventListener('input', () => {
                clearTimeout(timer);
                const term = customerSearch.value.trim();
                if (term.length < 2) {
                    state.searchToken++;
                    closeResults();
                    return;
                }
                customerResults.innerHTML = '<div class="customer-loading">Searching...</div>';
                customerResults.classList.add('open');
                const token = ++state.searchToken;
                timer = setTimeout(async () => {
                    try {
                        const response = await fetch(`/Bookings/SearchCustomers?query=${encodeURIComponent(term)}`);
                        if (!response.ok) throw new Error('Search failed');
                        const json = await response.json();
                        if (token !== state.searchToken) return;
                        if (!json.success) throw new Error(json.message || 'Search failed');
                        renderResults(json.customers || []);
                    } catch {
                        if (token !== state.searchToken) return;
                        customerResults.innerHTML = '<div class="customer-error">Search failed. Please try again.</div>';
                    }
                }, 250);
            });

            customerSearch.addEventListener('keydown', (event) => {
                if (!items.length) return;
                if (event.key === 'ArrowDown') {
                    event.preventDefault();
                    activeIndex = (activeIndex + 1) % items.length;
                    highlight();
                } else if (event.key === 'ArrowUp') {
                    event.preventDefault();
                    activeIndex = activeIndex <= 0 ? items.length - 1 : activeIndex - 1;
                    highlight();
                } else if (event.key === 'Enter' && activeIndex >= 0) {
                    event.preventDefault();
                    const customer = items[activeIndex];
                    selectCustomer({ id: customer.id, name: customer.name, phone: customer.phone, code: customer.code });
                } else if (event.key === 'Escape') {
                    closeResults();
                }
            });

            customerSearch.addEventListener('focus', () => {
                if (items.length) customerResults.classList.add('open');
            });

            customerClear?.addEventListener('click', () => {
                customerId.value = '';
                customerSearch.value = '';
                state.customerLabel = '';
                closeResults();
                customerClear.hidden = true;
                setFieldError(form, 'CustomerId', 'Select a customer for this booking.');
                if (customerSummary) {
                    customerSummary.innerHTML = '<i class="bi bi-person-badge"></i><p>Select a customer to preview profile, membership status, loyalty points and blacklist warnings.</p>';
                }
                updateLiveSummary(form, state);
                refreshPrice(form, state);
            });

            document.addEventListener('click', (event) => {
                if (!customerResults.contains(event.target) && event.target !== customerSearch) closeResults();
            });
        }

        /* ---------------- court cascade ---------------- */
        const clearSlotSelection = () => {
            if (startInput) startInput.value = '';
            if (endInput) endInput.value = '';
            if (slotGrid) slotGrid.querySelectorAll('.booking-slot.selected').forEach((node) => node.classList.remove('selected'));
            clearFieldError(form, 'StartTime');
        };

        const resetSelect = (select, placeholder) => {
            if (!select) return;
            select.innerHTML = `<option value="">${placeholder}</option>`;
        };

        sportSelect?.addEventListener('change', async () => {
            const sportId = sportSelect.value;
            resetSelect(facilitySelect, 'Select facility');
            resetSelect(courtSelect, 'Select court');
            if (facilitySelect) facilitySelect.value = '';
            if (courtSelect) courtSelect.value = '';
            clearSlotSelection();
            clearFieldError(form, 'FacilityId');
            clearFieldError(form, 'CourtId');
            if (!sportId) {
                updateLiveSummary(form, state);
                return;
            }
            try {
                const response = await fetch(`/Bookings/GetFacilitiesBySport?sportId=${encodeURIComponent(sportId)}`);
                const json = await response.json();
                if (!json.success) throw new Error(json.message || 'Failed');
                if (!facilitySelect) return;
                facilitySelect.innerHTML = '<option value="">Select facility</option>' + (json.facilities || [])
                    .map((item) => `<option value="${escapeHtml(item.id)}">${escapeHtml(item.name)}</option>`).join('');
            } catch {
                showToast('error', 'Something Went Wrong', 'Facilities could not be loaded. Please try again.');
            }
            updateLiveSummary(form, state);
        });

        facilitySelect?.addEventListener('change', async () => {
            const facilityId = facilitySelect.value;
            resetSelect(courtSelect, 'Select court');
            if (courtSelect) courtSelect.value = '';
            clearSlotSelection();
            clearFieldError(form, 'CourtId');
            updateLiveSummary(form, state);
            if (!facilityId || !sportSelect?.value) return;
            try {
                const response = await fetch(`/Bookings/GetCourts?sportId=${encodeURIComponent(sportSelect.value)}&facilityId=${encodeURIComponent(facilityId)}`);
                const json = await response.json();
                if (!json.success) throw new Error(json.message || 'Failed');
                if (!courtSelect) return;
                courtSelect.innerHTML = '<option value="">Select court</option>' + (json.courts || [])
                    .map((item) => `<option value="${escapeHtml(item.id)}">${escapeHtml(item.name)} (${escapeHtml(item.status)})</option>`).join('');
            } catch {
                showToast('error', 'Something Went Wrong', 'Courts could not be loaded. Please try again.');
            }
        });

        courtSelect?.addEventListener('change', () => {
            clearSlotSelection();
            clearFieldError(form, 'CourtId');
            loadSlots(form, state);
            updateLiveSummary(form, state);
        });

        dateInput?.addEventListener('change', () => {
            clearSlotSelection();
            clearFieldError(form, 'BookingDate');
            loadSlots(form, state);
            updateLiveSummary(form, state);
        });

        durationInput?.addEventListener('change', () => {
            if (!slotDurationVisible) return;
            clearSlotSelection();
            loadSlots(form, state);
        });

        startInput?.addEventListener('change', () => {
            updateEndTime(form);
            updateLiveSummary(form, state);
            refreshPrice(form, state);
        });

        discountInput?.addEventListener('input', () => refreshPrice(form, state));
        paidInput?.addEventListener('input', () => refreshPrice(form, state));
        statusSelect?.addEventListener('change', () => updateLiveSummary(form, state));

        /* ---------------- slots ---------------- */
        async function loadSlots(form, state) {
            if (!slotGrid) return;
            const courtId = courtValue();
            const date = dateInput?.value?.trim();
            if (!courtId || !date) {
                slotGrid.innerHTML = '<div class="booking-slot-empty">Choose a court and date to load available slots.</div>';
                if (slotStatus) slotStatus.textContent = '';
                return;
            }
            const token = ++state.slotToken;
            slotGrid.innerHTML = '<div class="booking-slot-empty">Loading available slots...</div>';
            const params = new URLSearchParams({ courtId, bookingDate: date });
            if (slotDurationVisible && durationInput?.value) params.set('durationMinutes', durationInput.value);
            if (bookingId()) params.set('bookingId', bookingId());
            try {
                const response = await fetch(`/Bookings/GetAvailableSlots?${params}`);
                const json = await response.json();
                if (token !== state.slotToken) return;
                if (!json.success) throw new Error(json.message || 'Failed');
                const slots = json.slots || [];
                if (json.durationMinutes > 0 && durationInput && !slotDurationVisible) durationInput.value = json.durationMinutes;
                if (slotHint && json.durationMinutes > 0) slotHint.textContent = `Slots are ${json.durationMinutes} minutes long and follow the court schedule.`;
                if (!slots.length) {
                    slotGrid.innerHTML = '<div class="booking-slot-empty">This court has no bookable slots on the selected date.</div>';
                    if (slotStatus) slotStatus.textContent = 'No schedule or pricing is configured for this court on the selected date.';
                    return;
                }
                slotGrid.innerHTML = slots.map((slot) => `
                    <button type="button" class="booking-slot ${slot.isAvailable ? '' : 'unavailable'}" data-slot="${escapeHtml(slot.startTime)}" data-slot-end="${escapeHtml(slot.endTime)}" data-slot-duration="${escapeHtml(slot.durationMinutes)}" ${slot.isAvailable ? '' : 'disabled'} title="${escapeHtml(slot.message || '')}">
                        <strong>${escapeHtml(slot.displayText)}</strong>
                        <small>${slot.isPeakRate ? 'Peak' : 'Standard'} &bull; ${escapeHtml(state.currency)} ${Number(slot.basePrice || 0).toLocaleString()}</small>
                    </button>`).join('');
                const available = slots.filter((slot) => slot.isAvailable).length;
                if (slotStatus) slotStatus.textContent = `${available} of ${slots.length} slots available. Unavailable slots are already booked or have no pricing.`;
                slotGrid.querySelectorAll('.booking-slot:not(.unavailable)').forEach((button) => {
                    button.addEventListener('click', () => {
                        if (startInput) startInput.value = button.dataset.slot;
                        if (durationInput && button.dataset.slotDuration) durationInput.value = button.dataset.slotDuration;
                        updateEndTime(form);
                        slotGrid.querySelectorAll('.booking-slot.selected').forEach((node) => node.classList.remove('selected'));
                        button.classList.add('selected');
                        clearFieldError(form, 'StartTime');
                        updateLiveSummary(form, state);
                        refreshPrice(form, state);
                    });
                });
            } catch (error) {
                if (token !== state.slotToken) return;
                slotGrid.innerHTML = '<div class="booking-slot-empty">Available slots could not be loaded. Please try again.</div>';
                if (slotStatus) slotStatus.textContent = error?.message || 'Available slots could not be loaded.';
            }
        }

        /* ---------------- pricing ---------------- */
        async function refreshPrice(form, state) {
            const customer = value('[data-customer-id]');
            const sport = sportSelect?.value?.trim() || q('[name="SportId"]')?.value?.trim() || '';
            const court = courtValue();
            const date = dateInput?.value?.trim();
            const start = startInput?.value?.trim();
            if (!customer || !sport || !court || !date || !start) {
                renderPrice(form, state, null, 'Select a customer, court, date and slot to see the price.');
                return;
            }
            const params = new URLSearchParams({
                customerId: customer,
                sportId: sport,
                courtId: court,
                bookingDate: date,
                startTime: start,
                manualDiscountAmount: discountInput?.value || '0',
                paidAmount: paidInput?.value || '0'
            });
            if (durationInput?.value) params.set('durationMinutes', durationInput.value);
            if (bookingId()) params.set('bookingId', bookingId());

            const token = ++state.priceToken;
            try {
                const response = await fetch(`/Bookings/CalculatePrice?${params}`);
                const json = await response.json();
                if (token !== state.priceToken) return;
                renderPrice(form, state, json.summary, json.message);
            } catch {
                if (token !== state.priceToken) return;
                renderPrice(form, state, null, 'The price could not be calculated. Please try again.');
            }
        }

        function renderPrice(form, state, summary, message) {
            const set = (selector, text) => {
                const element = form.querySelector(selector);
                if (element) element.textContent = text;
            };
            const format = (amount) => `${state.currency} ${Number(amount || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;

            if (!summary) {
                set('[data-price-base]', '—');
                set('[data-price-membership]', '—');
                set('[data-price-manual]', '—');
                set('[data-price-tax]', '—');
                set('[data-price-total]', '—');
                set('[data-price-paid]', '—');
                set('[data-price-balance]', '—');
                set('[data-summary-total]', '—');
                set('[data-summary-balance]', '—');
                if (priceNote) {
                    priceNote.textContent = message || '';
                    priceNote.hidden = !message;
                    priceNote.classList.toggle('is-error', Boolean(message));
                }
                return;
            }

            state.currency = summary.currencySymbol || state.currency;
            set('[data-price-base]', format(summary.baseAmount));
            set('[data-price-membership]', format(summary.membershipDiscountAmount));
            set('[data-price-manual]', format(summary.manualDiscountAmount));
            set('[data-price-tax]', format(summary.taxAmount));
            set('[data-price-total]', format(summary.totalAmount));
            set('[data-price-paid]', format(summary.paidAmount));
            set('[data-price-balance]', format(summary.balanceAmount));
            set('[data-summary-total]', format(summary.totalAmount));
            set('[data-summary-balance]', format(summary.balanceAmount));

            const notes = [summary.pricingError, summary.manualDiscountMessage, summary.paidAmountMessage, message].filter(Boolean);
            if (priceNote) {
                priceNote.textContent = notes.join(' ');
                priceNote.hidden = notes.length === 0;
                priceNote.classList.toggle('is-error', Boolean(summary.pricingError));
            }
            if (summary.pricingError) setFieldError(form, 'StartTime', summary.pricingError);
            else clearFieldError(form, 'StartTime');
            if (summary.manualDiscountMessage) setFieldError(form, 'ManualDiscountAmount', summary.manualDiscountMessage);
            if (summary.paidAmountMessage) setFieldError(form, 'PaidAmount', summary.paidAmountMessage);
        }

        /* ---------------- submit guard ---------------- */
        form.addEventListener('submit', (event) => {
            const problems = [];
            if (customerSearch && !value('[data-customer-id]')) {
                setFieldError(form, 'CustomerId', 'Select a customer for this booking.');
                problems.push('customer');
            }
            if (sportSelect && !sportSelect.value) {
                setFieldError(form, 'SportId', 'Select a sport.');
                problems.push('sport');
            }
            if (facilitySelect && !facilitySelect.value) {
                setFieldError(form, 'FacilityId', 'Select a facility.');
                problems.push('facility');
            }
            if (courtSelect && !courtSelect.value) {
                setFieldError(form, 'CourtId', 'Select a court.');
                problems.push('court');
            }
            if (dateInput && !dateInput.value) {
                setFieldError(form, 'BookingDate', 'Select a booking date.');
                problems.push('date');
            }
            if (startInput && startInput.type === 'hidden' && !startInput.value) {
                setFieldError(form, 'StartTime', 'Select an available slot.');
                problems.push('slot');
            }
            if (!problems.length) return;

            event.preventDefault();
            showToast('warning', 'Attention Required', 'Complete the highlighted fields before saving the booking.');
            const firstKey = { customer: 'CustomerId', sport: 'SportId', facility: 'FacilityId', court: 'CourtId', date: 'BookingDate', slot: 'StartTime' }[problems[0]];
            const anchor = form.querySelector(`[data-field-error="${firstKey}"]`);
            anchor?.scrollIntoView({ behavior: 'smooth', block: 'center' });
        });

        /* ---------------- initial state ---------------- */
        if (customerId?.value && customerSearch) {
            state.customerLabel = customerSearch.value || 'Selected customer';
            if (customerClear) customerClear.hidden = false;
            loadCustomerSummary(form, customerSummary, customerId.value, state);
        }
        updateEndTime(form);
        updateLiveSummary(form, state);
        loadSlots(form, state);
        refreshPrice(form, state);
    }

    async function loadCustomerSummary(form, panel, customerId, state) {
        if (!panel) return;
        try {
            const response = await fetch(`/Bookings/GetCustomerSummary?customerId=${encodeURIComponent(customerId)}`);
            const json = await response.json();
            if (!json.success || !json.customer) {
                panel.innerHTML = '<i class="bi bi-person-badge"></i><p>Customer details could not be loaded.</p>';
                return;
            }
            const customer = json.customer;
            const money = (amount) => `${state.currency} ${Number(amount || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
            const customerTypes = ['Individual', 'Corporate', 'Student', 'Family', 'Walk In', 'VIP'];
            const customerType = typeof customer.customerType === 'number'
                ? (customerTypes[customer.customerType] || 'Customer')
                : (customer.customerType || 'Customer');
            const membership = customer.membership
                ? `${customer.membership.plan} &bull; ${customer.membership.discountPercentage}% off`
                : 'No active membership';
            panel.classList.toggle('is-warning', Boolean(customer.isBlacklisted));
            panel.innerHTML = `
                <div class="booking-summary-head"><strong>${escapeHtml(customer.fullName)}</strong><span>${escapeHtml(customer.customerCode)} &bull; ${escapeHtml(customer.primaryPhone)}</span></div>
                <div class="booking-summary-grid">
                    <div><span>Type</span><strong>${escapeHtml(customerType)}</strong></div>
                    <div><span>Loyalty Points</span><strong>${escapeHtml(customer.loyaltyPoints)}</strong></div>
                    <div><span>Outstanding</span><strong>${escapeHtml(money(customer.outstandingBalance))}</strong></div>
                    <div><span>Membership</span><strong>${membership}</strong></div>
                </div>
                ${customer.isBlacklisted ? `<p class="booking-summary-alert"><i class="bi bi-exclamation-triangle"></i> Blacklisted customer${customer.blacklistReason ? `: ${escapeHtml(customer.blacklistReason)}` : ''}.</p>` : ''}`;
        } catch {
            panel.innerHTML = '<i class="bi bi-person-badge"></i><p>Customer details could not be loaded.</p>';
        }
    }

    function setFieldError(form, key, message) {
        const target = form.querySelector(`[data-field-error="${key}"]`);
        if (!target) return;
        target.textContent = message;
        target.hidden = false;
    }

    function clearFieldError(form, key) {
        const target = form.querySelector(`[data-field-error="${key}"]`);
        if (!target) return;
        target.textContent = '';
        target.hidden = true;
    }

    function showToast(type, title, message) {
        window.GameHubToast?.show?.({ type, title, message });
    }

    function updateEndTime(form) {
        const start = form.querySelector('[data-start-time]')?.value;
        const duration = Number(form.querySelector('[data-duration]')?.value || 0);
        const end = form.querySelector('[data-end-time]');
        if (!start || !duration || !end) return;
        const [h, m] = start.split(':').map(Number);
        const date = new Date(2000, 0, 1, h, m + duration);
        end.value = `${String(date.getHours()).padStart(2, '0')}:${String(date.getMinutes()).padStart(2, '0')}`;
    }

    function updateLiveSummary(form, state) {
        const set = (selector, text) => {
            const element = form.querySelector(selector);
            if (element) element.textContent = text;
        };
        const selectedText = (select) => select && select.selectedOptions.length ? select.selectedOptions[0].text : 'Not selected';
        const start = form.querySelector('[data-start-time]')?.value || '--:--';
        const end = form.querySelector('[data-end-time]')?.value || '--:--';
        const duration = Number(form.querySelector('[data-duration]')?.value || 0);

        set('[data-summary-customer]', state.customerLabel || 'Not selected');
        set('[data-summary-sport]', selectedText(form.querySelector('[data-sport-select]')));
        set('[data-summary-facility]', selectedText(form.querySelector('[data-facility-select]')));
        set('[data-summary-court]', selectedText(form.querySelector('[data-court-select]')));
        set('[data-summary-date]', formatBookingDate(form.querySelector('[data-booking-date]')?.value));
        set('[data-summary-time]', `${start} - ${end}`);
        set('[data-summary-duration]', duration > 0 ? `${duration} min` : 'Not selected');
        set('[data-summary-status]', selectedText(form.querySelector('[data-status-select]')));
    }

    function formatBookingDate(value) {
        if (!value) return 'Not selected';
        const parsed = new Date(`${value}T00:00:00`);
        if (Number.isNaN(parsed.getTime())) return value;
        return parsed.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
    }
})();
