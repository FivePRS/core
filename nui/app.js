const app = document.getElementById("app");
const home = document.getElementById("home");
const appGrid = document.getElementById("app-grid");
const appWindow = document.getElementById("app-window");
const wallpaper = document.getElementById("wallpaper");
const extFrame = document.getElementById("ext-frame");
const cardOverlay = document.getElementById("card-overlay");

const cardShowMs = 12000;

let appState = null;
let activeApp = null;
let offerTimer = null;
let cardTimer = null;
const appStates = {};

function iconFor(entry) {
  const icon = entry.icon ?? "";
  if (/^(https?|nui):\/\//.test(icon)) return iconImage(icon, "app-image");

  const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
  svg.setAttribute("viewBox", "0 0 24 24");
  svg.setAttribute("aria-hidden", "true");
  const path = document.createElementNS("http://www.w3.org/2000/svg", "path");
  path.setAttribute("d", icon);
  svg.append(path);
  return svg;
}

function availableApps(state) {
  return [...(state.apps ?? [])].sort((a, b) => a.order - b.order);
}

function findApp(id) {
  return appState ? availableApps(appState).find((entry) => entry.id === id) ?? null : null;
}

function preferences() {
  return appState?.terminal?.preferences ?? {};
}

function applyPreferences(prefs) {
  document.documentElement.dataset.textSize = prefs.textSize ?? "m";
  document.documentElement.dataset.reduceMotion = String(Boolean(prefs.reduceMotion));
}

function renderHomeApps(state) {
  appGrid.replaceChildren(...availableApps(state).map((entry) => {
    const tile = el("button", { className: "app-tile", dataset: { app: entry.id } }, [
      el("span", { className: "app-icon" }, [iconFor(entry), entry.badge ? el("span", { className: "app-badge", text: entry.badge }) : null]),
      el("span", { className: "app-label", text: entry.label }),
    ]);
    tile.type = "button";
    tile.querySelector(".app-icon").style.setProperty("--tile", entry.color);
    return tile;
  }));
}

function formatShift(startedAt) {
  const seconds = Math.max(0, Math.floor((Date.now() - startedAt) / 1000));
  const minutes = Math.floor(seconds / 60);
  return `${Math.floor(minutes / 60)}:${String(minutes % 60).padStart(2, "0")}:${String(seconds % 60).padStart(2, "0")}`;
}

function tickShift() {
  if (appState?.unit && appState.home?.shiftStartedAt) setText("home-shift-time", formatShift(appState.home.shiftStartedAt));
}

function formatDistance(meters) {
  if (meters === null || meters === undefined) return "";
  const miles = meters / 1609.34;
  return miles < 0.1 ? "Nearby" : `${miles.toFixed(1)} mi`;
}

function tickOffer() {
  const offer = appState?.home?.offer;
  if (!offer) return;
  const remaining = Math.max(0, offer.expiresAt - Date.now());
  setText("home-offer-countdown", `${Math.ceil(remaining / 1000)}s`);
  document.getElementById("home-offer-bar").style.width = `${((remaining / Math.max(1, offer.windowMs)) * 100).toFixed(1)}%`;
}

function renderHomeWidgets(state) {
  const homeState = state.home ?? {};
  const offer = homeState.offer;

  document.getElementById("home-offer").hidden = !offer;
  clearInterval(offerTimer);
  offerTimer = null;
  if (offer) {
    setText("home-offer-title", `#${offer.id} ${offer.name} · Code ${offer.code}`);
    setText("home-offer-detail", [offer.description, formatDistance(offer.distanceM)].filter(Boolean).join(" · "));
    for (const button of document.querySelectorAll("[data-home]")) button.disabled = false;
    tickOffer();
    offerTimer = setInterval(tickOffer, 250);
  }

  document.getElementById("home-shift").hidden = !state.unit;
  if (state.unit) {
    setText("home-units", homeState.units ?? 0);
    setText("home-calls", homeState.calls ?? 0);
    setText("home-shift-time", homeState.shiftStartedAt ? formatShift(homeState.shiftStartedAt) : "0:00:00");
  }
}

function renderStatusBar(state) {
  const unit = state.unit;
  const identity = document.getElementById("sb-identity");
  identity.replaceChildren(...(unit
    ? [el("span", { className: "sb-callsign", text: unit.callsign }), el("span", { className: "sb-agency", text: unit.agency })]
    : [document.createTextNode(state.identity ?? "")]));

  const status = document.getElementById("sb-status");
  status.hidden = !unit;
  if (unit) {
    status.textContent = unit.status.replace(/([a-z])([A-Z])/g, "$1 $2");
    status.dataset.status = unit.status;
  }
}

function renderWallpaper(terminal) {
  const url = terminal?.wallpaper ?? "";
  if (wallpaper.getAttribute("src") === url) return;
  wallpaper.hidden = !url;
  if (url) wallpaper.src = url;
}

function pageUrl(entry) {
  return `${entry.page}${entry.page.includes("?") ? "&" : "?"}app=${encodeURIComponent(entry.id)}`;
}

function sendToFrame(type, payload) {
  extFrame.contentWindow?.postMessage({ fiveprs: true, kind: "event", type, payload }, "*");
}

function leaveApp() {
  if (!activeApp && !extFrame.getAttribute("src")) return;
  extFrame.removeAttribute("src");
  extFrame.hidden = true;
  extView.replaceChildren();
  post("appOpen", { id: "" });
}

function openApp(id) {
  const entry = findApp(id);
  if (!entry) return;

  leaveApp();
  activeApp = entry.id;
  home.hidden = true;
  appWindow.hidden = false;
  setText("app-title", entry.label);

  const icon = document.getElementById("app-icon");
  icon.replaceChildren(iconFor(entry));
  icon.style.setProperty("--tile", entry.color);

  extView.hidden = Boolean(entry.page);
  extFrame.hidden = !entry.page;
  if (entry.page) extFrame.src = pageUrl(entry);
  post("appOpen", { id: entry.id });
}

function goHome() {
  leaveApp();
  activeApp = null;
  appWindow.hidden = true;
  home.hidden = false;
}

function renderApp(state, opening) {
  appState = state;
  applyPreferences(preferences());
  const logo = document.getElementById("brand-logo");
  if (state.nameplate && logo.getAttribute("src") !== state.nameplate) logo.src = state.nameplate;
  setDepartmentIcons(state.icons);
  renderWallpaper(state.terminal);
  renderStatusBar(state);
  renderHomeApps(state);
  renderHomeWidgets(state);
  tickClock();

  if (opening) {
    const requested = findApp(state.defaultApp);
    if (requested) openApp(requested.id);
    else goHome();
  } else if (activeApp && !availableApps(state).some((entry) => entry.id === activeApp)) {
    goHome();
  }

  const active = findApp(activeApp);
  if (active && !active.page && state.appView?.app === active.id) renderAppView(state.appView);
  if (active?.page) sendToFrame("context", state.context);
}

function tickClock() {
  const now = new Date();
  const hour12 = !preferences().clock24;
  document.getElementById("clock").textContent = now.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit", hour12 });
  const hours = hour12 ? now.getHours() % 12 || 12 : now.getHours();
  setText("home-time", `${hour12 ? hours : String(hours).padStart(2, "0")}:${String(now.getMinutes()).padStart(2, "0")}`);
  setText("home-date", [now.toLocaleDateString([], { weekday: "long", day: "numeric", month: "long" }), appState?.home?.location].filter(Boolean).join(" · "));
}

function showCard(card) {
  clearTimeout(cardTimer);
  setText("card-caption", `${card.firstName} ${card.lastName} shows you their ${card.license.name}`);
  document.getElementById("card-slot").replaceChildren(licenseCard(card, { small: true }));
  cardOverlay.hidden = false;
  cardTimer = setTimeout(() => {
    cardOverlay.hidden = true;
  }, cardShowMs);
}

screens.app = {
  open(state) {
    app.hidden = false;
    renderApp(state, true);
  },
  update(state) {
    if (!app.hidden) renderApp(state, false);
  },
  appMessage(message) {
    if (message.type === "state") appStates[message.app] = message.payload;
    if (message.app === activeApp) sendToFrame(message.type, message.payload);
  },
  close() {
    leaveApp();
    clearInterval(offerTimer);
    offerTimer = null;
    app.hidden = true;
  },
};

screens.card = {
  show: showCard,
};

window.addEventListener("message", (event) => {
  const message = event.data;
  if (event.source !== extFrame.contentWindow || !message || message.fiveprs !== true) return;

  if (message.kind === "ready") {
    sendToFrame("context", appState?.context ?? null);
    if (activeApp in appStates) sendToFrame("state", appStates[activeApp]);
  }
  else if (message.kind === "home") goHome();
  else if (message.kind === "action") appAction(activeApp, message.action, message.data);
});

wallpaper.addEventListener("error", () => {
  wallpaper.hidden = true;
});

appGrid.addEventListener("click", (event) => {
  const tile = event.target.closest("[data-app]");
  if (tile) openApp(tile.dataset.app);
});

document.getElementById("home-offer").addEventListener("click", (event) => {
  const button = event.target.closest("[data-home]");
  if (!button || button.disabled) return;
  for (const node of document.querySelectorAll("[data-home]")) node.disabled = true;
  appAction("dispatch", button.dataset.home);
});

document.getElementById("home-button").addEventListener("click", goHome);
document.getElementById("home-key").addEventListener("click", goHome);
document.getElementById("close").addEventListener("click", () => post("appClose"));

document.addEventListener("keydown", (event) => {
  if (event.key !== "Escape" || app.hidden) return;
  if (activeApp) goHome();
  else post("appClose");
});

setInterval(tickClock, 15000);
setInterval(tickShift, 1000);
