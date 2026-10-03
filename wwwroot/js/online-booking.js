(function () {
    const root = document.querySelector('[data-online-booking]');
    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, char => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;' }[char]));
    const toast = (type, title, message) => {
        if (window.GameHubToast) window.GameHubToast.show({ type, title, message });
        else alert(message);
    };

    if (root) {
        const form = document.querySelector('[data-booking-form]');
        const dateInput = document.querySelector('[data-booking-date]');
        const durationHidden = document.querySelector('[data-duration-hidden]');
        const selectedStart = document.querySelector('[data-selected-start]');
        const slotGrid = document.querySelector('[data-slot-grid]');
        const slotState = document.querySelector('[data-slot-state]');
        const playerCount = document.querySelector('[data-player-count]');
        const token = form?.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
        const currency = document.querySelector('[data-summary-total]')?.textContent || '';
        const customerPanel = document.querySelector('[data-customer-panel]');

        document.querySelectorAll('[data-quick-date]').forEach((button) => {
            button.addEventListener('click', () => {
                dateInput.value = button.dataset.quickDate;
                loadSlots();
            });
        });

        document.querySelectorAll('[data-duration]').forEach((button) => {
            button.addEventListener('click', () => {
                document.querySelectorAll('[data-duration]').forEach((item) => item.classList.remove('selected'));
                button.classList.add('selected');
                durationHidden.value = button.dataset.duration;
                selectedStart.value = '';
                loadSlots();
            });
        });

        dateInput?.addEventListener('change', () => { loadSlots(); loadMembershipSummary(); });
        playerCount?.addEventListener('input', () => {
            document.querySelector('[data-summary-players]').textContent = playerCount.value || '1';
            calculatePrice();
        });

        async function loadSlots() {
            if (!dateInput?.value || !durationHidden?.value) return;
            slotState.textContent = 'Loading live availability...';
            slotGrid.innerHTML = Array.from({ length: 6 }).map(() => '<button class="slot-card" disabled><strong>Loading</strong><span>Please wait</span></button>').join('');
            try {
                const url = `${root.dataset.availabilityUrl}?courtId=${root.dataset.courtId}&bookingDate=${dateInput.value}&durationMinutes=${durationHidden.value}`;
                const response = await fetch(url);
                const data = await response.json();
                if (!data.success) {
                    slotState.textContent = data.message || 'Could not load slots.';
                    slotGrid.innerHTML = '';
                    return;
                }

                slotState.textContent = data.slots.length ? 'Select one of the available times below.' : 'No schedule or available slots for this selection.';
                slotGrid.innerHTML = data.slots.map((slot) => `
                    <button type="button" class="slot-card" ${slot.isAvailable ? '' : 'disabled'} data-slot-start="${escapeHtml(slot.startTime)}" data-slot-display="${escapeHtml(slot.displayTime)}" data-slot-price="${escapeHtml(slot.formattedPrice)}">
                        <strong>${escapeHtml(slot.displayTime)}</strong>
                        <span class="slot-price">${escapeHtml(slot.formattedPrice)}</span>
                        <small>${slot.isAvailable ? 'Available' : (escapeHtml(slot.unavailableReason) || 'Unavailable')}</small>
                        <em class="slot-badge">${escapeHtml(slot.pricingLabel)}</em>
                    </button>`).join('');

                slotGrid.querySelectorAll('[data-slot-start]').forEach((button) => {
                    button.addEventListener('click', () => {
                        slotGrid.querySelectorAll('.slot-card').forEach((item) => item.classList.remove('selected'));
                        button.classList.add('selected');
                        selectedStart.value = button.dataset.slotStart;
                        document.querySelector('[data-summary-date]').textContent = dateInput.value;
                        document.querySelector('[data-summary-time]').textContent = button.dataset.slotDisplay;
                        document.querySelector('[data-summary-pricing]').textContent = button.dataset.slotPrice;
                        calculatePrice();
                    });
                });
            } catch {
                slotState.textContent = 'Availability could not be loaded. Please try again.';
                slotGrid.innerHTML = '';
            }
        }

        async function loadCustomerSummary() {
            if (!customerPanel) return;
            const url = root.dataset.customerUrl;
            if (!url) {
                customerPanel.innerHTML = '<span class="summary-empty">No account summary available.</span>';
                return;
            }
            customerPanel.innerHTML = '<span class="loading-line">Loading customer summary...</span>';
            try {
                const response = await fetch(url);
                if (!response.ok) throw new Error(`Failed to load customer summary (${response.status})`);
                const data = await response.json();
                renderCustomerSummary(data);
            } catch {
                customerPanel.innerHTML = '<span class="summary-empty">Could not load your account summary. Please refresh to try again.</span>';
            }
        }

        function renderCustomerSummary(data) {
            const account = data && data.success === true ? data.customer : null;
            if (!account) {
                const message = (data && data.message) || 'No account summary available. Please sign in to continue.';
                customerPanel.innerHTML = `<span class="summary-empty">${escapeHtml(message)}</span>`;
                return;
            }
            customerPanel.innerHTML = `
                <i class="bi bi-person-badge"></i>
                <div class="customer-summary-fields">
                    <h3>${escapeHtml(account.fullName || 'Customer')}</h3>
                    <dl>
                        <div><dt>Code</dt><dd>${escapeHtml(account.customerCode || '—')}</dd></div>
                        <div><dt>Email</dt><dd>${escapeHtml(account.email || '—')} <small class="${account.emailVerified ? 'summary-verified' : 'summary-unverified'}">${account.emailVerified ? 'Verified' : 'Unverified'}</small></dd></div>
                        <div><dt>Phone</dt><dd>${escapeHtml(account.primaryPhone || '—')}</dd></div>
                        <div><dt>Membership</dt><dd>${escapeHtml(account.membershipStatus || 'Non-Member')}</dd></div>
                        <div><dt>Loyalty Points</dt><dd>${escapeHtml(String(account.loyaltyPoints ?? 0))}</dd></div>
                    </dl>
                    <p class="summary-membership-note" data-summary-membership>Checking membership...</p>
                </div>`;
            loadMembershipSummary();
        }

        async function loadMembershipSummary() {
            const line = document.querySelector('[data-summary-membership]');
            const url = root.dataset.membershipUrl;
            if (!line || !url || !root.dataset.sportId || !dateInput?.value) return;
            const params = new URLSearchParams({ sportId: root.dataset.sportId, bookingDate: dateInput.value, isPeakRate: 'false' });
            try {
                const response = await fetch(`${url}?${params}`);
                if (!response.ok) throw new Error(`Failed to load membership summary (${response.status})`);
                const data = await response.json();
                const membership = data && data.success === true ? data.membership : null;
                line.classList.remove('is-error');
                line.textContent = membership
                    ? membership.applies
                        ? `${membership.plan || 'Membership'} — ${membership.discountPercentage ?? 0}% off · ${membership.remainingHours ?? 0}h left · expires ${membership.expiryDate || 'N/A'}`
                        : (membership.message || 'No active membership.')
                    : (data && data.message) || 'No membership benefits available.';
            } catch {
                line.classList.add('is-error');
                line.textContent = 'Membership benefits unavailable.';
            }
        }

        async function calculatePrice() {
            if (!selectedStart.value) return;
            const body = new URLSearchParams(new FormData(form));
            body.set('StartTime', selectedStart.value);
            body.set('DurationMinutes', durationHidden.value);
            body.set('__RequestVerificationToken', token);
            try {
                const response = await fetch(root.dataset.calculateUrl, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                    body
                });
                const data = await response.json();
                if (!data.success) {
                    document.querySelector('[data-summary-total]').textContent = data.message || 'Sign in to calculate';
                    return;
                }
                const price = data.price;
                const symbol = price.currencySymbol || 'Rs';
                document.querySelector('[data-summary-base]').textContent = `${symbol} ${Number(price.baseAmount).toLocaleString()}`;
                document.querySelector('[data-summary-discount]').textContent = `- ${symbol} ${Number(price.membershipDiscountAmount).toLocaleString()}`;
                document.querySelector('[data-summary-tax]').textContent = `${symbol} ${Number(price.taxAmount).toLocaleString()}`;
                document.querySelector('[data-summary-total]').textContent = `${symbol} ${Number(price.totalAmount).toLocaleString()}`;
            } catch {
                document.querySelector('[data-summary-total]').textContent = 'Unable to calculate';
            }
        }

        form?.addEventListener('submit', (event) => {
            if (!selectedStart.value) {
                event.preventDefault();
                toast('warning', 'Select a Slot', 'Please select an available time slot before review.');
                return;
            }
            const button = document.querySelector('[data-review-submit]');
            if (button) {
                button.disabled = true;
                button.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Rechecking...';
            }
        });

        loadSlots();
        loadCustomerSummary();
    }

    const countdown = document.querySelector('[data-checkout-countdown]');
    if (countdown) {
        let remaining = Number(countdown.dataset.checkoutCountdown || 0);
        const render = () => {
            if (remaining <= 0) {
                countdown.textContent = 'Expired';
                countdown.classList.add('is-expired');
                const cta = document.querySelector('[data-checkout-continue]');
                if (cta) {
                    cta.removeAttribute('href');
                    cta.setAttribute('aria-disabled', 'true');
                    cta.setAttribute('tabindex', '-1');
                    cta.classList.add('disabled');
                    cta.textContent = 'Session Expired';
                }
                return;
            }
            const minutes = Math.floor(remaining / 60);
            const seconds = remaining % 60;
            countdown.textContent = `${minutes}:${seconds.toString().padStart(2, '0')} remaining`;
            remaining -= 1;
            window.setTimeout(render, 1000);
        };
        render();
    }

    document.querySelectorAll('.btn-premium, .btn-ghost-light').forEach((button) => {
        button.addEventListener('click', (event) => {
            const ripple = document.createElement('span');
            ripple.className = 'button-ripple';
            const rect = button.getBoundingClientRect();
            ripple.style.left = `${event.clientX - rect.left}px`;
            ripple.style.top = `${event.clientY - rect.top}px`;
            button.appendChild(ripple);
            window.setTimeout(() => ripple.remove(), 650);
        });
    });
})();
