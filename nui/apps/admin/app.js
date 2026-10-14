const filterInput = document.getElementById("player-filter");
const query = document.getElementById("query");

const filters = {
  all: () => true,
  duty: (player) => player.onDuty,
  down: (player) => player.down,
};

let view = null;
let filled = false;
let section = "players";
let filter = "all";
let selectedLicense = null;

function button(text, className, dataset) {
  const node = el("button", { className, text, dataset });
  node.type = "button";
  return node;
}

function departmentName(id) {
  return (view?.departments ?? []).find((department) => department.id === id)?.name ?? "None";
}

function stateCell(player) {
  if (player.down) return el("span", { className: "state-down", text: "Down" });
  return player.onDuty ? el("span", { className: "state-duty", text: "On duty" }) : el("span", { className: "state-off", text: "Off duty" });
}

function playerSub(player) {
  if (player.onDuty) return [player.agency || departmentName(player.department), player.callsign].filter(Boolean).join(" · ");
  return player.department ? departmentName(player.department) : "No department";
}

function rosterToggles(player) {
  return el("div", { className: "roster-toggles" }, view.departments.map((department) => {
    const granted = player.departments.includes(department.id);
    return button(department.name, `button small${granted ? " active" : ""}`, { license: player.license, department: department.id, granted: String(!granted) });
  }));
}

function renderPlayers() {
  const term = filterInput.value.trim().toLowerCase();
  const players = view.state.online
    .filter(filters[filter])
    .filter((player) => !term || player.name.toLowerCase().includes(term) || String(player.serverId) === term || player.callsign.toLowerCase().includes(term));

  if (!view.state.online.some((player) => player.license === selectedLicense)) selectedLicense = view.state.online[0]?.license ?? null;

  document.getElementById("players").replaceChildren(...players.map((player) => {
    const row = el("tr", { className: player.license === selectedLicense ? "selected" : "", dataset: { player: player.license } }, [
      el("td", { className: "mono", text: player.serverId }),
      el("td", {}, [el("span", { className: "player-name", text: player.name }), el("span", { className: "player-sub", text: playerSub(player) })]),
      el("td", { text: player.rankName || "—" }),
      el("td", {}, [stateCell(player)]),
    ]);
    return row;
  }));
  document.getElementById("players-empty").hidden = players.length > 0;
  renderPanel(view.state.online.find((player) => player.license === selectedLicense));
}

function renderPanel(player) {
  document.getElementById("panel").hidden = !player;
  if (!player) return;

  setText("panel-meta", `Player ${player.serverId} · ${player.onDuty ? "On duty" : "Off duty"}`);
  setText("panel-name", player.name);
  setText("panel-license", player.license);

  const department = departmentName(player.department);
  const ranks = view.ranks?.[department] ?? view.ranks?.Police ?? [];
  const rank = document.getElementById("rank");
  rank.replaceChildren(...ranks.map((name, index) => {
    const option = el("option", { text: `${index + 1} · ${name}` });
    option.value = String(index + 1);
    option.selected = name === player.rankName;
    return option;
  }));

  const departmentSelect = document.getElementById("department");
  departmentSelect.replaceChildren(...view.departments.map((item) => {
    const option = el("option", { text: item.name });
    option.value = item.name;
    option.selected = item.id === player.department;
    return option;
  }));

  document.getElementById("roster").replaceChildren(...view.departments.map((item) => {
    const granted = player.departments.includes(item.id);
    const toggle = el("button", { className: "switch", dataset: { license: player.license, department: item.id, granted: String(!granted) } });
    toggle.type = "button";
    toggle.setAttribute("role", "switch");
    toggle.setAttribute("aria-checked", String(granted));
    toggle.setAttribute("aria-label", `${item.name} roster`);
    return el("div", { className: "switch-row" }, [el("span", { text: item.name }), toggle]);
  }));

  for (const node of document.querySelectorAll("[data-act]")) node.disabled = false;
  document.querySelector('[data-act="revive"]').disabled = !player.down;
  document.querySelector('[data-act="offduty"]').disabled = !player.onDuty;
}

function renderRoster() {
  const results = view.state.results;
  document.getElementById("results").replaceChildren(...results.map((player) => el("tr", {}, [
    el("td", {}, [el("span", { className: "player-name", text: player.name }), el("span", { className: "player-sub", text: `Last seen ${formatDate(player.lastSeen)}` })]),
    el("td", { text: player.rankName || "—" }),
    el("td", { className: "muted", text: departmentName(player.department) }),
    el("td", {}, [rosterToggles(player)]),
  ])));
  document.getElementById("results-empty").hidden = results.length > 0;
}

function renderCalls() {
  const calls = view.state.calls;
  document.getElementById("calls").replaceChildren(...calls.map((call) => el("tr", {}, [
    el("td", { className: "mono", text: `#${call.id}` }),
    el("td", {}, [el("span", { className: "player-name", text: call.name }), el("span", { className: "player-sub", text: `Code ${call.code} · ${departmentName(call.department)}${call.emergency ? " · 911" : ""}` })]),
    el("td", { className: "muted", text: call.territory ?? "—" }),
    el("td", { className: "mono", text: call.units.join(", ") || "None" }),
    el("td", {}, [button("End", "button small danger", { endcall: call.id })]),
  ])));
  document.getElementById("calls-empty").hidden = calls.length > 0;
  setText("nav-calls", calls.length || "");
}

function renderAudit() {
  const entries = view.state.audit;
  document.getElementById("audit").replaceChildren(...entries.map((entry) => el("tr", {}, [
    el("td", { className: "mono muted", text: new Date(entry.createdAt).toLocaleString([], { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" }) }),
    el("td", { className: "mono", text: entry.action }),
    el("td", { text: entry.actorName }),
    el("td", { className: "muted", text: entry.details }),
  ])));
  document.getElementById("audit-empty").hidden = entries.length > 0;
}

function renderServer() {
  document.getElementById("facts").replaceChildren(...view.state.server.map((fact) => el("div", {}, [
    el("dt", { className: "eyebrow", text: fact.label }),
    el("dd", { text: fact.value }),
  ])));

  const addons = view.state.addons ?? [];
  setText("addon-count", `${addons.length} running`);
  document.getElementById("addons-empty").hidden = addons.length > 0;
  document.getElementById("addons").replaceChildren(...addons.map((addon) => el("li", { className: "addon" }, [
    el("span", { className: "addon-title" }, [
      el("span", { text: addon.name }),
      addon.official ? el("span", { className: "pill info", text: "Official" }) : null,
      el("span", { className: "pill", text: addon.kind }),
    ]),
    el("span", { className: "addon-meta", text: [addon.version && `v${addon.version}`, addon.author].filter(Boolean).join(" · ") }),
    addon.description ? el("p", { className: "addon-description", text: addon.description }) : null,
  ])));
  document.getElementById("warnings").replaceChildren(...view.state.warnings.map((warning) => el("p", { className: "banner warn", text: warning })));
}

function showSection(next) {
  section = next;
  for (const node of document.querySelectorAll("[data-section]")) node.classList.toggle("active", node.dataset.section === next);
  for (const name of ["players", "roster", "calls", "audit", "server"]) document.getElementById(`section-${name}`).hidden = name !== next;
}

function render(next) {
  view = next;
  if (!filled) {
    filled = true;
    query.value = next.state.query ?? "";
  }

  document.getElementById("unrestricted").hidden = next.state.restrictDepartments;
  setText("nav-players", next.state.online.length);
  renderPlayers();
  renderRoster();
  renderCalls();
  renderAudit();
  renderServer();
}

function act(action, target, value = "") {
  FivePRS.action("action", { action, target: String(target), value: String(value) });
}

FivePRS.on("state", render);

filterInput.addEventListener("input", () => view && renderPlayers());

document.getElementById("search").addEventListener("submit", (event) => {
  event.preventDefault();
  FivePRS.action("search", { query: query.value.trim() });
});

document.addEventListener("click", (event) => {
  const row = event.target.closest("tr[data-player]");
  if (row) {
    selectedLicense = row.dataset.player;
    renderPlayers();
    return;
  }

  const target = event.target.closest("button");
  if (!target || target.disabled || !view) return;
  const player = view.state.online.find((item) => item.license === selectedLicense);

  if (target.dataset.section) {
    showSection(target.dataset.section);
  } else if (target.dataset.filter) {
    filter = target.dataset.filter;
    for (const node of document.querySelectorAll("[data-filter]")) node.classList.toggle("active", node === target);
    renderPlayers();
  } else if (target.dataset.license) {
    target.disabled = true;
    FivePRS.action("roster", { license: target.dataset.license, departmentId: Number(target.dataset.department), granted: target.dataset.granted === "true" });
  } else if (target.dataset.endcall) {
    target.disabled = true;
    act("endcall", target.dataset.endcall);
  } else if (target.dataset.act && player) {
    const value = target.dataset.act === "setrank" ? document.getElementById("rank").value
      : target.dataset.act === "addxp" ? document.getElementById("xp").value
      : target.dataset.act === "setdept" ? document.getElementById("department").value
      : "";
    target.disabled = true;
    act(target.dataset.act, player.serverId, value);
  }
});
