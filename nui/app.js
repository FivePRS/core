const app = document.getElementById("app");
const tabsNav = document.getElementById("tabs");

const tabLabels = {
  duty: "Duty",
  dispatch: "Dispatch",
  civilian: "Civilian",
};

let activeTab = null;

function selectTab(tab) {
  activeTab = tab;
  for (const button of tabsNav.querySelectorAll("button")) {
    const selected = button.dataset.tab === tab;
    button.classList.toggle("active", selected);
    button.setAttribute("aria-selected", String(selected));
  }
  for (const panel of app.querySelectorAll(".tab")) panel.hidden = panel.id !== `tab-${tab}`;
}

function renderTabs(tabs) {
  tabsNav.replaceChildren(
    ...tabs.map((tab) => el("button", { className: "tab-button", text: tabLabels[tab] ?? tab, dataset: { tab } }))
  );
  for (const button of tabsNav.querySelectorAll("button")) button.setAttribute("role", "tab");
}

function renderApp(state, opening) {
  renderTabs(state.tabs);
  if (opening || !state.tabs.includes(activeTab)) activeTab = state.defaultTab;
  selectTab(activeTab);
  renderDuty(state.duty, opening);
  renderDispatch(state.dispatch);
  renderCivilian(state.civilian, opening);
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
  civilianError(message) {
    showCivilianError(message);
    if (civilianView) renderCivilian(civilianView, false);
  },
  close() {
    app.hidden = true;
    stopOfferTimer();
  },
};

tabsNav.addEventListener("click", (event) => {
  const target = event.target.closest("button");
  if (target?.dataset.tab) selectTab(target.dataset.tab);
});

document.getElementById("close").addEventListener("click", () => post("appClose"));

document.addEventListener("keydown", (event) => {
  if (event.key === "Escape" && !app.hidden) post("appClose");
});

setInterval(tickClock, 15000);
