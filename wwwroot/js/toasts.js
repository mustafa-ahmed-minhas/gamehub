// Global GameHub toast system with subtle Web Audio chimes.
(function () {
    const region = document.querySelector("[data-toast-region]");
    if (!region) {
        return;
    }

    const durations = { success: 4000, info: 5000, warning: 6000, error: 7000 };
    const titles = {
        success: "Success",
        error: "Something Went Wrong",
        warning: "Attention Required",
        info: "Information"
    };
    const icons = {
        success: "bi-check2-circle",
        error: "bi-x-circle",
        warning: "bi-exclamation-triangle",
        info: "bi-info-circle"
    };

    let audioContext = null;
    let lastSoundAt = 0;

    const getSoundEnabled = () => localStorage.getItem("gamehubToastSound") !== "off";
    const setSoundEnabled = (enabled) => {
        localStorage.setItem("gamehubToastSound", enabled ? "on" : "off");
        updateSoundToggle();
    };

    const ensureAudio = () => {
        const AudioContext = window.AudioContext || window.webkitAudioContext;
        if (!AudioContext) {
            return null;
        }

        audioContext ||= new AudioContext();
        return audioContext;
    };

    const playTone = (frequency, start, duration, volume) => {
        const ctx = ensureAudio();
        if (!ctx) {
            return;
        }

        const oscillator = ctx.createOscillator();
        const gain = ctx.createGain();
        oscillator.type = "sine";
        oscillator.frequency.setValueAtTime(frequency, ctx.currentTime + start);
        gain.gain.setValueAtTime(0.0001, ctx.currentTime + start);
        gain.gain.exponentialRampToValueAtTime(volume, ctx.currentTime + start + 0.035);
        gain.gain.exponentialRampToValueAtTime(0.0001, ctx.currentTime + start + duration);
        oscillator.connect(gain);
        gain.connect(ctx.destination);
        oscillator.start(ctx.currentTime + start);
        oscillator.stop(ctx.currentTime + start + duration + 0.03);
    };

    const playSound = (type) => {
        if (!getSoundEnabled() || Date.now() - lastSoundAt < 260) {
            return;
        }

        try {
            const ctx = ensureAudio();
            if (!ctx) {
                return;
            }

            if (ctx.state === "suspended") {
                ctx.resume().catch(() => {});
            }

            lastSoundAt = Date.now();
            if (type === "success") {
                playTone(587, 0, 0.18, 0.025);
                playTone(784, 0.13, 0.24, 0.022);
            } else if (type === "error") {
                playTone(220, 0, 0.18, 0.021);
                playTone(174, 0.15, 0.22, 0.018);
            } else if (type === "warning") {
                playTone(440, 0, 0.28, 0.019);
            } else {
                playTone(698, 0, 0.22, 0.014);
            }
        } catch {
            // Browser audio policies may block playback; the toast remains silent.
        }
    };

    const removeToast = (toast) => {
        toast.classList.add("removing");
        window.setTimeout(() => toast.remove(), 190);
    };

    window.GameHubToast = {
        show(options) {
            const type = ["success", "error", "warning", "info"].includes(options?.type) ? options.type : "info";
            const duration = durations[type];
            const toast = document.createElement("div");
            toast.className = `gamehub-toast ${type}`;
            toast.setAttribute("role", type === "error" ? "alert" : "status");
            toast.style.setProperty("--toast-duration", `${duration}ms`);

            toast.innerHTML = `
                <span class="toast-icon"><i class="bi ${icons[type]}"></i></span>
                <span class="toast-copy"><strong></strong><span></span></span>
                <button type="button" class="toast-close" aria-label="Close notification"><i class="bi bi-x-lg"></i></button>
                <span class="toast-progress"></span>`;

            toast.querySelector(".toast-copy strong").textContent = options?.title || titles[type];
            toast.querySelector(".toast-copy span").textContent = options?.message || "";
            toast.querySelector(".toast-close").addEventListener("click", () => removeToast(toast));
            toast.addEventListener("mouseenter", () => toast.classList.add("paused"));
            toast.addEventListener("mouseleave", () => toast.classList.remove("paused"));

            region.appendChild(toast);
            playSound(type);
            window.setTimeout(() => removeToast(toast), duration);
        }
    };

    const updateSoundToggle = () => {
        document.querySelectorAll("[data-toast-sound-toggle]").forEach((button) => {
            const enabled = getSoundEnabled();
            button.querySelector("i").className = enabled ? "bi bi-volume-up" : "bi bi-volume-mute";
            button.querySelector("span").textContent = enabled ? "Toast Sounds On" : "Toast Sounds Off";
        });
    };

    document.addEventListener("click", (event) => {
        const button = event.target.closest("[data-toast-sound-toggle]");
        if (button) {
            setSoundEnabled(!getSoundEnabled());
        }
    });

    updateSoundToggle();

    const serverToast = document.querySelector("[data-server-toast]");
    if (serverToast) {
        try {
            window.GameHubToast.show(JSON.parse(serverToast.textContent));
        } catch {
            // Ignore malformed toast payloads.
        }
    }
})();
