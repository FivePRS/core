const app = document.getElementById("app");
const home = document.getElementById("home");
const appGrid = document.getElementById("app-grid");
const appWindow = document.getElementById("app-window");
const wallpaper = document.getElementById("wallpaper");

const appIcons = {
  duty: "M12 2l8 3v6c0 5.2-3.4 9.4-8 11-4.6-1.6-8-5.8-8-11V5z M12 8l1.2 2.6 2.8.3-2.1 1.9.6 2.8L12 14.2l-2.5 1.4.6-2.8-2.1-1.9 2.8-.3z",
  dispatch: "M12 10a2 2 0 1 1 0 4 2 2 0 0 1 0-4z M7.8 7.8a6 6 0 0 0 0 8.4 M16.2 7.8a6 6 0 0 1 0 8.4 M4.9 4.9a10 10 0 0 0 0 14.2 M19.1 4.9a10 10 0 0 1 0 14.2",
  records: "M3 6a1 1 0 0 1 1-1h5l2 2h9a1 1 0 0 1 1 1v10a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1z M14.5 14.5l2.5 2.5 M13 15a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5z",
  characters: "M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8z M4 21a8 8 0 0 1 16 0",
  licenses: "M3 6a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z M8.5 14a2 2 0 1 0 0-4 2 2 0 0 0 0 4z M5.5 17a3 3 0 0 1 6 0 M14 10h4 M14 14h3",
  vehicles: "M5 17H3v-4l2-5h14l2 5v4h-2 M3 13h18 M7.5 19a2 2 0 1 0 0-4 2 2 0 0 0 0 4z M16.5 19a2 2 0 1 0 0-4 2 2 0 0 0 0 4z",
  emergency: "M5 4h4l2 5-2.5 1.5a11 11 0 0 0 5 5L15 13l5 2v4a2 2 0 0 1-2 2A16 16 0 0 1 3 6a2 2 0 0 1 2-2z",
  admin: "M4 21v-7 M4 10V3 M12 21v-9 M12 8V3 M20 21v-5 M20 12V3 M1 14h6 M9 8h6 M17 16h6",
  settings: "M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6z M12 2v3 M12 19v3 M4.9 4.9l2.1 2.1 M17 17l2.1 2.1 M2 12h3 M19 12h3 M4.9 19.1L7 17 M17 7l2.1-2.1",
};

const appCatalog = [
  { id: "duty", tab: "duty", label: "Duty", color: "#335cff" },
  { id: "dispatch", tab: "dispatch", label: "Dispatch", color: "#e81010" },
  { id: "records", tab: "records", label: "Records", color: "#4f46e5" },
  { id: "characters", tab: "civilian", mode: "characters", label: "Characters", color: "#0d9488" },
  { id: "licenses", tab: "civilian", mode: "licenses", label: "Licenses", color: "#d97706" },
  { id: "vehicles", tab: "civilian", mode: "vehicles", label: "Vehicles", color: "#475569" },
  { id: "emergency", tab: "emergency", label: "911", color: "#b91c1c" },
  { id: "admin", tab: "admin", label: "Admin", color: "#7c3aed" },
  { id: "settings", tab: "settings", label: "Settings", color: "#334155" },
];

let appState = null;
let activeApp = null;

function iconSvg(name) {
  const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
  svg.setAttribute("viewBox", "0 0 24 24");
  svg.setAttribute("aria-hidden", "true");
  const path = document.createElementNS("http://www.w3.org/2000/svg", "path");
  path.setAttribute("d", appIcons[name] ?? "");
  svg.append(path);
  return svg;
}

function availableApps(state) {
  return appCatalog.filter((entry) => entry.id === "settings" || state.tabs.includes(entry.tab));
}

function badgeFor(entry, state) {
  if (entry.id === "dispatch" && state.dispatch?.offer) return "!";
  if (entry.id === "dispatch" && state.dispatch?.activeCall) return "1";
  if (entry.id === "emergency" && state.emergency?.status) return "•";
  return null;
}

function renderHome(state) {
  appGrid.replaceChildren(...availableApps(state).map((entry) => {
    const badge = badgeFor(entry, state);
    const tile = el("button", { className: "app-tile", dataset: { app: entry.id } }, [
      el("span", { className: "app-icon" }, [iconSvg(entry.id)]),
      el("span", { className: "app-label", text: entry.label }),
      badge ? el("span", { className: "app-badge", text: badge }) : null,
    ]);
    tile.type = "button";
    tile.querySelector(".app-icon").style.setProperty("--tile", entry.color);
    return tile;
  }));
}

function renderStatusBar(state) {
  const unit = state.dispatch?.self;
  const civilian = state.civilian?.state;
  const character = civilian?.characters?.find((entry) => entry.id === civilian.activeCharacterId);

  setText("sb-identity", unit
    ? `${unit.callsign} · ${unit.agency}`
    : character ? `${character.firstName} ${character.lastName}` : state.duty?.name ?? "");

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

function selectTab(tab) {
  for (const panel of app.querySelectorAll(".tab")) panel.hidden = panel.id !== `tab-${tab}`;
}

function openApp(id) {
  const entry = appCatalog.find((item) => item.id === id);
  if (!entry || !availableApps(appState).includes(entry)) return;

  activeApp = entry.id;
  home.hidden = true;
  appWindow.hidden = false;
  setText("app-title", entry.label);

  const icon = document.getElementById("app-icon");
  icon.replaceChildren(iconSvg(entry.id));
  icon.style.setProperty("--tile", entry.color);

  document.getElementById("tab-civilian").dataset.mode = entry.mode ?? "";
  selectTab(entry.tab);
}

function goHome() {
  activeApp = null;
  appWindow.hidden = true;
  home.hidden = false;
}

function renderApp(state, opening) {
  appState = state;
  const logo = document.getElementById("brand-logo");
  if (state.nameplate && logo.getAttribute("src") !== state.nameplate) logo.src = state.nameplate;
  setDepartmentIcons(state.icons);
  renderWallpaper(state.terminal);
  renderStatusBar(state);
  renderHome(state);

  if (opening) {
    const requested = appCatalog.find((entry) => entry.tab === state.defaultTab);
    if (requested) openApp(requested.id);
    else goHome();
  } else if (activeApp && !availableApps(state).some((entry) => entry.id === activeApp)) {
    goHome();
  }

  renderDuty(state.duty, opening);
  renderDispatch(state.dispatch);
  renderRecords(state.records, opening);
  renderCivilian(state.civilian, opening);
  renderEmergency(state.emergency, opening);
  renderAdmin(state.admin, opening);
  renderSettings(state.terminal, state.dispatch?.self);
}

function tickClock() {
  document.getElementById("clock").textContent = new Date().toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
}

screens.app = {
  open(state) {
    app.hidden = false;
    tickClock();
    renderApp(state, true);
  },
  update(state) {
    if (!app.hidden) renderApp(state, false);
  },
  rejected(message) {
    showDutyError(message);
  },
  recordsError(message) {
    showRecordsError(message);
    if (recordsView) renderRecords(recordsView, false);
  },
  emergencyError(message) {
    showEmergencyError(message);
  },
  civilianError(message) {
    showCivilianError(message);
    if (civilianView) renderCivilian(civilianView, false);
  },
  close() {
    app.hidden = true;
    stopOfferTimer();
  },
};

wallpaper.addEventListener("error", () => {
  wallpaper.hidden = true;
});

appGrid.addEventListener("click", (event) => {
  const tile = event.target.closest("[data-app]");
  if (tile) openApp(tile.dataset.app);
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
