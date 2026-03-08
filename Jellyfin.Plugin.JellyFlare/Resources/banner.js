(function () {
    "use strict";

    // Prevent double-execution (JS Injector + direct <script> tag).
    if (document.getElementById("jf-jellyflare")) return;

    var CONFIG = null; // loaded asynchronously from /JellyFlare/config

    var BANNER_H = 36;
    var BANNER_H_MOBILE = 42;
    var TRANSITION_MS = 300; // kept in sync with transitionSpeed after config load
    var rotationTimer = null;
    var dismissedMessages = new Set();
    var dismissAll = false;
    var shuffledQueue = [];
    var currentMessage = null;
    var isPermanent = false;
    var isInPause = false;

    var STORAGE_KEY = "jf-dismissed-v1";

    function isAdminPage() {
        return /\b(dashboard|configurationpage|users|useredit|userprofiles|networking|devices|playback|dlna|notifications|libraries|metadata|subtitles|log|scheduledtasks|apikeys|activity|plugins|encodingsettings|streamingsettings)\b/.test(window.location.hash);
    }

    function checkTimeWindow(now, timeStart, timeEnd) {
        if (!timeStart && !timeEnd) return true;
        var nowMins = now.getHours() * 60 + now.getMinutes();
        if (timeStart) {
            var sp = timeStart.split(':');
            if (nowMins < parseInt(sp[0]) * 60 + parseInt(sp[1])) return false;
        }
        if (timeEnd) {
            var ep = timeEnd.split(':');
            if (nowMins > parseInt(ep[0]) * 60 + parseInt(ep[1])) return false;
        }
        return true;
    }

    function isInSchedule(msg) {
        var sch = msg.schedule;
        if (!sch || !sch.type || sch.type === 'always') return true;
        var now = new Date();
        if (sch.type === 'fixed') {
            if (sch.fixedStart) { var s = new Date(sch.fixedStart); if (isNaN(s) || now < s) return false; }
            if (sch.fixedEnd) { var e = new Date(sch.fixedEnd); if (isNaN(e) || now > e) return false; }
            return true;
        }
        if (sch.type === 'annual') {
            var ms = sch.monthStart, ds = sch.dayStart, me = sch.monthEnd, de = sch.dayEnd;
            if (!ms || !ds || !me || !de) return checkTimeWindow(now, sch.timeStart, sch.timeEnd);
            var nowMD = (now.getMonth() + 1) * 100 + now.getDate();
            var startMD = ms * 100 + ds, endMD = me * 100 + de;
            var inRange = startMD <= endMD
                ? nowMD >= startMD && nowMD <= endMD
                : nowMD >= startMD || nowMD <= endMD;
            return inRange && checkTimeWindow(now, sch.timeStart, sch.timeEnd);
        }
        if (sch.type === 'weekly') {
            if (!sch.weekDays || sch.weekDays.indexOf(now.getDay()) === -1) return false;
            return checkTimeWindow(now, sch.timeStart, sch.timeEnd);
        }
        if (sch.type === 'daily') {
            return checkTimeWindow(now, sch.timeStart, sch.timeEnd);
        }
        return true;
    }

    // --- Queue builder (shuffle or sequential based on config) ---
    function buildQueue() {
        var eligible = CONFIG.rotationMessages.filter(function (m) {
            return m.text && m.enabled !== false && isInSchedule(m) && !dismissedMessages.has(m.text);
        });
        if (CONFIG.rotationShuffle !== false) {
            // Fisher-Yates shuffle
            for (var i = eligible.length - 1; i > 0; i--) {
                var j = Math.floor(Math.random() * (i + 1));
                var tmp = eligible[i]; eligible[i] = eligible[j]; eligible[j] = tmp;
            }
        }
        return eligible;
    }

    // --- Persist dismissed messages to localStorage ---
    function getPersistedDismissed() {
        try { return JSON.parse(localStorage.getItem(STORAGE_KEY) || "[]"); } catch (e) { return []; }
    }

    function savePersistedDismissed() {
        try {
            var arr = [];
            dismissedMessages.forEach(function (t) { arr.push(t); });
            localStorage.setItem(STORAGE_KEY, JSON.stringify(arr));
        } catch (e) { /* localStorage unavailable */ }
    }

    // --- CSS (uses CSS custom properties for config-driven values) ---
    var root = document.documentElement;
    var style = document.createElement("style");
    style.id = "jf-banner-style";
    style.textContent = [
        ":root {",
        "  --jf-h: " + BANNER_H + "px;",
        "  --jf-h-m: " + BANNER_H_MOBILE + "px;",
        "  --jf-tr: opacity .3s ease,transform .3s ease;",
        "  --jf-fs: 14px;",
        "  --jf-fs-m: 13px;",
        "}",
        "#jf-jellyflare {",
        "  position:fixed; top:0; left:0; width:100%; z-index:999999;",
        "  text-align:center; padding:0 70px; font-weight:bold; font-size:var(--jf-fs);",
        "  box-sizing:border-box; opacity:0; transform:translateY(-100%);",
        "  transition:var(--jf-tr);",
        "  display:flex; align-items:center; justify-content:center;",
        "  height:var(--jf-h);",
        "}",
        "#jf-jellyflare.visible { opacity:1; transform:translateY(0); }",
        "#jf-jellyflare.off { display:none!important; }",
        "#jf-banner-text { color:inherit; text-decoration:none; }",
        "@media(max-width:600px){",
        "  #jf-jellyflare { font-size:var(--jf-fs-m); padding:0 36px; height:var(--jf-h-m); }",
        "  #jf-banner-dismiss-all { display:none!important; }",
        "  #jf-banner-close { font-size:22px; padding:4px 8px; }",
        "  #jf-banner-close-area { right:4px; }",
        "}",
        "#jf-banner-close-area {",
        "  position:absolute; right:8px; top:50%; transform:translateY(-50%);",
        "  display:flex; flex-direction:row; align-items:center; gap:6px;",
        "}",
        "#jf-banner-close {",
        "  background:none; border:none; font-size:18px; cursor:pointer;",
        "  opacity:.6; transition:opacity .2s; padding:0 4px; line-height:1;",
        "}",
        "#jf-banner-close:hover { opacity:1; }",
        "#jf-banner-dismiss-all {",
        "  background:none; border:none; font-size:9px; cursor:pointer;",
        "  opacity:.45; transition:opacity .2s; padding:0; line-height:1;",
        "  text-decoration:underline; white-space:nowrap;",
        "}",
        "#jf-banner-dismiss-all:hover { opacity:1; }",
        "#jf-jellyflare.permanent #jf-banner-close-area { display:none!important; }",
        "body.jf-banner-active .skinHeader { top:var(--jf-h)!important; transition:top .3s ease; }",
        "body.jf-banner-active .mainDrawer { top:var(--jf-h)!important; height:calc(100% - var(--jf-h))!important; transition:top .3s ease,height .3s ease; }",
        "body.jf-banner-active .skinBody { padding-top:var(--jf-h)!important; transition:padding-top .3s ease; }",
        "@media(max-width:600px){",
        "  body.jf-banner-active .skinHeader { top:var(--jf-h-m)!important; }",
        "  body.jf-banner-active .mainDrawer { top:var(--jf-h-m)!important; height:calc(100% - var(--jf-h-m))!important; }",
        "  body.jf-banner-active .skinBody { padding-top:var(--jf-h-m)!important; }",
        "}",
        ".skinHeader,.mainDrawer,.skinBody { transition:top .3s ease,height .3s ease,padding-top .3s ease; }",
        "body.hide-scroll #jf-jellyflare { display:none!important; }",
        "body.hide-scroll .skinHeader { top:0!important; }",
        "body.hide-scroll .mainDrawer { top:0!important; height:100%!important; }",
        "body.hide-scroll .skinBody { padding-top:0!important; }",
    ].join("\n");
    document.head.appendChild(style);

    // --- DOM ---
    var banner = document.createElement("div");
    banner.id = "jf-jellyflare";
    banner.classList.add("off");

    // textSpan is an <a> so it can optionally be a clickable link
    var textSpan = document.createElement("a");
    textSpan.id = "jf-banner-text";

    var closeArea = document.createElement("div");
    closeArea.id = "jf-banner-close-area";

    var closeBtn = document.createElement("button");
    closeBtn.id = "jf-banner-close";
    closeBtn.innerHTML = "✕";
    closeBtn.title = "Fermer cette annonce";
    closeBtn.addEventListener("click", dismissCurrent);

    var dismissAllBtn = document.createElement("button");
    dismissAllBtn.id = "jf-banner-dismiss-all";
    dismissAllBtn.textContent = "tout masquer";
    dismissAllBtn.title = "Masquer toutes les annonces pour cette session";
    dismissAllBtn.addEventListener("click", dismissAllMessages);

    closeArea.appendChild(dismissAllBtn);
    closeArea.appendChild(closeBtn);
    banner.appendChild(textSpan);
    banner.appendChild(closeArea);
    // NOTE: banner is NOT inserted into the DOM here.
    // It is inserted just before tick() runs (after the async config fetch),
    // so the Jellyfin SPA has finished mounting and won't remove it.

    // --- Actions ---
    function dismissCurrent() {
        if (isPermanent || !currentMessage) return;
        dismissedMessages.add(currentMessage.text);
        if (CONFIG && CONFIG.persistDismiss) {
            savePersistedDismissed();
        }
        fadeOutThenNext();
    }

    function dismissAllMessages() {
        if (isPermanent) return;
        dismissAll = true;
        fadeOutThenHide();
    }

    function fadeOutThenHide() {
        banner.classList.remove("visible");
        setTimeout(hideBanner, TRANSITION_MS);
    }

    function fadeOutThenNext() {
        banner.classList.remove("visible");
        setTimeout(function () {
            hideBanner();
            clearTimeout(rotationTimer);
            // Go to pause phase, not next message
            isInPause = true;
            var wait = CONFIG.pauseDuration > 0 ? CONFIG.pauseDuration * 1000 : 50;
            rotationTimer = setTimeout(tick, wait);
        }, TRANSITION_MS);
    }

    function showBanner(msg, permanent) {
        if (!msg || !msg.text) { hideBanner(); return; }
        if (!banner.isConnected) { document.body.prepend(banner); }
        currentMessage = msg;
        isPermanent = !!permanent;
        isInPause = false;

        textSpan.textContent = msg.text;
        var safeUrl = /^(https?:\/\/|\/)/i;
        if (msg.url && safeUrl.test(msg.url)) {
            textSpan.href = msg.url;
            textSpan.target = "_blank";
            textSpan.rel = "noopener noreferrer";
            textSpan.style.cursor = "pointer";
            textSpan.style.textDecoration = "underline";
        } else {
            textSpan.removeAttribute("href");
            textSpan.removeAttribute("target");
            textSpan.removeAttribute("rel");
            textSpan.style.cursor = "";
            textSpan.style.textDecoration = "";
        }
        banner.style.background = msg.bg || "#1976d2";
        banner.style.color = msg.color || "#fff";
        closeBtn.style.color = msg.color || "#fff";
        dismissAllBtn.style.color = msg.color || "#fff";

        banner.classList.remove("off");
        if (permanent) banner.classList.add("permanent");
        else banner.classList.remove("permanent");
        document.body.classList.add("jf-banner-active");

        requestAnimationFrame(function () {
            requestAnimationFrame(function () {
                banner.classList.add("visible");
            });
        });
    }

    function hideBanner() {
        currentMessage = null;
        banner.classList.remove("visible");
        banner.classList.add("off");
        banner.classList.remove("permanent");
        document.body.classList.remove("jf-banner-active");
    }

    // --- Main loop ---
    function tick() {
        if (CONFIG.showInDashboard === false && isAdminPage()) { hideBanner(); return; }

        // Permanent override
        var po = CONFIG.permanentOverride;
        if (po && po.enabled !== false && po.activeIndex >= 0) {
            var entry = po.entries && po.entries[po.activeIndex];
            if (entry && entry.text && isInSchedule(entry)) {
                showBanner(entry, true);
                rotationTimer = setTimeout(tick, CONFIG.displayDuration * 1000);
                return;
            }
        }

        if (dismissAll || CONFIG.rotationEnabled === false) { hideBanner(); return; }

        // Currently showing a message → go to pause
        if (!isInPause && currentMessage) {
            banner.classList.remove("visible");
            setTimeout(hideBanner, TRANSITION_MS);
            isInPause = true;
            if (CONFIG.pauseDuration > 0) {
                rotationTimer = setTimeout(tick, CONFIG.pauseDuration * 1000);
            } else {
                rotationTimer = setTimeout(tick, 50);
            }
            return;
        }

        // In pause or first run → pick next message
        isInPause = false;

        if (shuffledQueue.length === 0) {
            shuffledQueue = buildQueue();
            if (shuffledQueue.length === 0) { hideBanner(); return; }
        }

        var msg = shuffledQueue.shift();

        // Re-check in case schedule/dismiss changed
        if (!msg || !msg.text || !isInSchedule(msg) || dismissedMessages.has(msg.text)) {
            // Try next in queue immediately
            rotationTimer = setTimeout(tick, 50);
            return;
        }

        showBanner(msg, false);
        rotationTimer = setTimeout(tick, CONFIG.displayDuration * 1000);
    }

    // --- Cleanup old CSS variables ---
    ["--banner-display", "--banner-height", "--banner-text", "--banner-bg", "--banner-color"]
        .forEach(function (p) { root.style.removeProperty(p); });

    // --- Go ---
    // Banner is for registered users only — require a valid Jellyfin auth token.
    var token = window.ApiClient ? window.ApiClient.accessToken() : null;
    if (!token) return;

    fetch("/JellyFlare/config", {
        headers: { "Authorization": "MediaBrowser Token=\"" + token + "\"" }
    })
        .then(function (r) { return r.ok ? r.json() : null; })
        .then(function (config) {
            if (!config) return;
            CONFIG = config;

            // --- Banner height ---
            var h = Math.max(24, Math.min(80, CONFIG.bannerHeight || 36));
            var hm = h + 6;
            root.style.setProperty("--jf-h", h + "px");
            root.style.setProperty("--jf-h-m", hm + "px");
            BANNER_H = h;
            BANNER_H_MOBILE = hm;

            // --- Transition speed ---
            var speedMap = { none: 0, fast: 150, normal: 300, slow: 600 };
            TRANSITION_MS = speedMap.hasOwnProperty(CONFIG.transitionSpeed) ? speedMap[CONFIG.transitionSpeed] : 300;
            var dur = (TRANSITION_MS / 1000).toFixed(2) + "s";
            root.style.setProperty("--jf-tr", "opacity " + dur + " ease,transform " + dur + " ease");

            // --- Font size ---
            var fs = Math.max(10, Math.min(32, CONFIG.fontSize || 14));
            root.style.setProperty("--jf-fs", fs + "px");
            root.style.setProperty("--jf-fs-m", Math.max(fs - 1, 10) + "px");

            // --- Font weight ---
            banner.style.fontWeight = CONFIG.fontBold !== false ? "bold" : "normal";

            // --- Text alignment ---
            if (CONFIG.textAlign === "left") {
                banner.style.justifyContent = "flex-start";
                banner.style.textAlign = "left";
                banner.style.paddingLeft = "16px";
                banner.style.paddingRight = "80px";
            }

            // --- Persist dismissed ---
            if (CONFIG.persistDismiss) {
                getPersistedDismissed().forEach(function (t) { dismissedMessages.add(t); });
            }

            // Apply control visibility
            if (CONFIG.showDismissButton === false) closeBtn.style.display = "none";
            if (CONFIG.dismissButtonSize) closeBtn.style.fontSize = CONFIG.dismissButtonSize + "px";
            if (CONFIG.showDismissAll === false) dismissAllBtn.style.display = "none";
            if (CONFIG.dismissAllSize) dismissAllBtn.style.fontSize = CONFIG.dismissAllSize + "px";
            dismissAllBtn.textContent = CONFIG.dismissAllText || "hide all";
            // Insert banner now: SPA has finished mounting so the div won't be evicted.
            if (!banner.isConnected) { document.body.prepend(banner); }

            // Re-evaluate on every SPA navigation.
            // Jellyfin uses hash-based routing for most transitions but also calls
            // pushState/replaceState directly for some navigations (e.g. home→admin).
            // All three sources are needed. The debounce collapses any burst of
            // concurrent events into a single evaluation, preventing flash cycles.
            var navTimer = null;
            function onNavigate() {
                clearTimeout(navTimer);
                navTimer = setTimeout(function () {
                    if (CONFIG.showInDashboard === false) {
                        clearTimeout(rotationTimer);
                        if (isAdminPage()) { hideBanner(); } else { tick(); }
                    }
                }, 50);
            }
            window.addEventListener("hashchange", onNavigate);
            window.addEventListener("popstate", onNavigate);
            (function () {
                function wrap(method) {
                    var orig = history[method];
                    history[method] = function () {
                        orig.apply(this, arguments);
                        onNavigate();
                    };
                }
                wrap('pushState');
                wrap('replaceState');
            }());
            tick();
        })
        .catch(function (err) {
            console.warn("[JellyFlare] Failed to load config:", err);
        });
})();
