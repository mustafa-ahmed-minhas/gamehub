(function(){const debounce=(fn,ms=450)=>{let t;return(...a)=>{clearTimeout(t);t=setTimeout(()=>fn(...a),ms)}};document.querySelectorAll('[data-sales-search]').forEach(i=>i.addEventListener('input',debounce(()=>i.form?.submit())));document.querySelectorAll('[data-sales-filter]').forEach(i=>i.addEventListener('change',()=>i.form?.submit()));document.querySelector('[data-sales-guidance-toggle]')?.addEventListener('click',e=>{const p=e.currentTarget.closest('[data-sales-guidance]');p?.classList.toggle('is-collapsed');localStorage.setItem('gamehub-sales-guidance-collapsed',p?.classList.contains('is-collapsed')?'true':'false')});document.querySelectorAll('[data-sales-guidance]').forEach(p=>{if(localStorage.getItem('gamehub-sales-guidance-collapsed')==='true')p.classList.add('is-collapsed')});const form=document.querySelector('[data-sales-form]');if(form){let dirty=false;form.addEventListener('input',()=>{dirty=true;calc()});form.addEventListener('submit',()=>dirty=false);window.addEventListener('beforeunload',e=>{if(dirty){e.preventDefault();e.returnValue=''}});document.querySelectorAll('[data-add-line]').forEach(b=>b.addEventListener('click',()=>addLine(b.dataset.type||'CustomService')));document.addEventListener('click',e=>{const btn=e.target.closest('[data-remove-line]');if(btn){btn.closest('[data-sales-line]')?.remove();renumber();calc()}});calc()}function money(n){const sym=document.querySelector('[name=CurrencySymbol]')?.value||'Rs';return `${sym} ${Number(n||0).toLocaleString(undefined,{minimumFractionDigits:2,maximumFractionDigits:2})}`}function calc(){let subtotal=0,disc=0,tax=0,items=0;document.querySelectorAll('[data-sales-line]').forEach(line=>{const q=+line.querySelector('[data-line-quantity]')?.value||0,p=+line.querySelector('[data-line-price]')?.value||0,d=+line.querySelector('[data-line-discount]')?.value||0,t=+line.querySelector('[data-line-tax]')?.value||0;const sub=q*p;const da=sub*d/100;const tx=Math.max(0,sub-da)*t/100;const total=Math.max(0,sub-da+tx);subtotal+=sub;disc+=da;tax+=tx;if((line.querySelector('[data-line-description]')?.value||'').trim())items++;const out=line.querySelector('[data-line-total]');if(out)out.value=total.toFixed(2)});const manual=+document.querySelector('[data-manual-discount]')?.value||0;const grand=Math.max(0,subtotal-disc-manual+tax);set('[data-total-subtotal]',money(subtotal));set('[data-total-line-discount]',money(disc));set('[data-total-tax]',money(tax));set('[data-total-grand]',money(grand));set('[data-summary-grand]',money(grand));set('[data-summary-items]',items);document.querySelector('[data-complete-line]')?.classList.toggle('complete',items>0);document.querySelector('[data-complete-terms]')?.classList.toggle('complete',!!document.querySelector('[data-summary-terms]')?.value?.trim());document.querySelector('[data-complete-validity]')?.classList.toggle('complete',!!document.querySelector('[data-summary-valid]')?.value)}function set(sel,val){const e=document.querySelector(sel);if(e)e.textContent=val}function addLine(type){const tpl=document.getElementById('salesLineTemplate');const wrap=document.querySelector('[data-sales-lines]');if(!tpl||!wrap)return;const index=wrap.querySelectorAll('[data-sales-line]').length;let html=tpl.innerHTML.replaceAll('__index__',index).replaceAll('__line__',index+1);wrap.insertAdjacentHTML('beforeend',html);const line=wrap.lastElementChild;const sel=line?.querySelector('[data-item-type]');if(sel)sel.value=type;const desc=line?.querySelector('[data-line-description]');if(desc)desc.value=type==='Membership'?'Membership package':type==='CourtBooking'?'Court booking service':'Custom service';calc()}function renumber(){document.querySelectorAll('[data-sales-line]').forEach((line,i)=>{const head=line.querySelector('.sales-cycle-line-head strong')||line.querySelector('.sales-line-head strong');if(head)head.textContent=`Line ${i+1}`;const num=line.querySelector('input[name$=".LineNumber"]');if(num)num.value=i+1})}document.querySelectorAll('[data-sales-counter]').forEach(el=>{const n=Number((el.textContent||'0').replace(/\\D/g,''));if(!n)return;let s=0;const step=()=>{s+=Math.max(1,Math.ceil(n/24));el.textContent=Math.min(s,n).toLocaleString();if(s<n)requestAnimationFrame(step)};requestAnimationFrame(step)})})();

(function () {
    const drawer = document.querySelector('[data-sales-filter-drawer]');
    const filterToggle = document.querySelector('[data-sales-filter-toggle]');
    const filterClose = document.querySelector('[data-sales-filter-close]');
    const filterCount = document.querySelector('[data-sales-filter-count]');

    const closeDrawer = () => {
        drawer?.classList.remove('is-open');
        drawer?.setAttribute('aria-hidden', 'true');
    };

    const openDrawer = () => {
        drawer?.classList.add('is-open');
        drawer?.setAttribute('aria-hidden', 'false');
        drawer?.querySelector('select, input, button')?.focus();
    };

    const updateFilterCount = () => {
        if (!filterCount) return;
        const active = Array.from(document.querySelectorAll('[data-sales-filter-input]'))
            .filter(input => `${input.value || ''}`.trim().length > 0).length;
        filterCount.textContent = active.toString();
        filterCount.toggleAttribute('hidden', active === 0);
    };

    filterToggle?.addEventListener('click', openDrawer);
    filterClose?.addEventListener('click', closeDrawer);
    drawer?.addEventListener('click', event => {
        if (event.target === drawer) closeDrawer();
    });
    document.querySelectorAll('[data-sales-filter-input]').forEach(input => {
        input.addEventListener('change', updateFilterCount);
        input.addEventListener('input', updateFilterCount);
    });
    updateFilterCount();

    const closeMenus = except => {
        document.querySelectorAll('[data-sales-menu].is-open').forEach(menu => {
            if (menu !== except) menu.classList.remove('is-open', 'drop-up');
        });
    };

    document.addEventListener('click', event => {
        const menuButton = event.target.closest('[data-sales-menu-button]');
        if (menuButton) {
            event.preventDefault();
            event.stopPropagation();
            const menu = menuButton.parentElement?.querySelector('[data-sales-menu]');
            if (!menu) return;
            const opening = !menu.classList.contains('is-open');
            closeMenus(menu);
            menu.classList.toggle('is-open', opening);
            menuButton.setAttribute('aria-expanded', opening ? 'true' : 'false');
            if (opening) {
                requestAnimationFrame(() => {
                    const rect = menu.getBoundingClientRect();
                    menu.classList.toggle('drop-up', rect.bottom > window.innerHeight - 16);
                });
            }
            return;
        }

        if (!event.target.closest('[data-sales-menu]')) closeMenus();
    });

    document.addEventListener('keydown', event => {
        if (event.key !== 'Escape') return;
        closeMenus();
        closeDrawer();
    });
})();

(function () {
    const form = document.querySelector('[data-sales-form]');
    if (!form) return;

    const saveBar = form.querySelector('[data-save-bar]');
    const meter = form.querySelector('[data-complete-meter]');
    const meterValue = form.querySelector('[data-complete-value]');

    form.querySelectorAll('input,select,textarea').forEach(input => {
        input.addEventListener('change', () => saveBar?.classList.add('is-dirty'));
        input.addEventListener('input', () => saveBar?.classList.add('is-dirty'));
    });

    form.addEventListener('submit', () => {
        form.querySelectorAll('[data-loading-button]').forEach(button => button.setAttribute('disabled', 'disabled'));
    });

    const updateCompleteness = () => {
        if (!meter || !meterValue) return;
        const checks = Array.from(form.querySelectorAll('.sales-cycle-completeness span'));
        if (!checks.length) return;
        const complete = checks.filter(span => span.classList.contains('complete')).length;
        const percent = Math.round((complete / checks.length) * 100);
        meter.setAttribute('value', percent.toString());
        meterValue.textContent = `${percent}%`;
    };

    form.addEventListener('input', updateCompleteness);
    form.addEventListener('change', updateCompleteness);
    updateCompleteness();
})();
