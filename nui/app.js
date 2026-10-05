const resource = typeof GetParentResourceName === "function" ? GetParentResourceName() : "fiveprs";

const statusLabels = {
  Available: "Available",
  EnRoute: "En route",
  OnScene: "On scene",
  Busy: "Busy",
};

const root = document.getElementById("mdt");
let view = null;

function post(name, data = {}) {
  return fetch(`https://${resource}/${name}`, {
    method: "POST",
    headers: { "Content-Type": "application/json; charset=UTF-8" },
    body: JSON.stringify(data),
  }).catch(() => {});
}

function el(tag, options = {}, children = []) {
  const node = document.createElement(tag);
  if (options.className) node.className = options.className;
  if (options.text !== undefined && options.text !== null) node.textContent = String(options.text);
  if (options.dataset) Object.assign(node.dataset, options.dataset);
  if (options.disabled) node.disabled = true;
  for (const child of children) if (child) node.append(child);
  return node;
}

function statusPill(status) {
  return el("span", { className: `status status-${status.toLowerCase()}`, text: statusLabels[status] ?? status });
}

function setText(id, value) {
  document.getElementById(id).textContent = value ?? "—";
}

function renderSelf(self) {
  setText("self-callsign", self.callsign);
  setText("self-name", self.name);
  setText("self-agency", self.agency);
  setText("self-territory", self.territory ?? "Unincorporated");
  setText("self-call", self.callId ? `#${self.callId}` : "None");

  const status = document.getElementById("self-status");
  status.replaceChildren(statusPill(self.status));

  const leadsCall = (view.calls ?? []).some((call) => call.isPrimary);
  for (const button of document.querySelectorAll("[data-status]")) {
    button.classList.toggle("active", button.dataset.status === self.status);
    button.disabled = leadsCall;
  }
  document.getElementById("status-hint").textContent = leadsCall
    ? "End your active callout to change status."
    : "En route and on scene update automatically from your position.";
}

function renderCalls(calls, self) {
  const body = document.getElementById("calls");
  const rows = calls.map((call) => {
    const mine = self.callId === call.id;
    const canAttach = !self.callId;

    const actions = el("div", { className: "actions" }, [
      call.hasLocation
        ? el("button", { className: "button small", text: "Waypoint", dataset: { action: "waypoint", call: call.id } })
        : null,
      mine
        ? null
        : el("button", {
            className: "button small",
            text: "Attach",
            dataset: { action: "attach", call: call.id },
            disabled: !canAttach,
          }),
    ]);

    const row = el("tr", { className: mine ? "mine" : "" }, [
      el("td", { className: "mono", text: `#${call.id}` }),
      el("td", {}, [
        el("span", { text: call.name }),
        el("span", { className: `sub code-${call.code}`, text: `Code ${call.code}` }),
      ]),
      el("td", { className: "muted", text: call.territory ?? "—" }),
      el("td", {}, [el("div", { className: "chips" }, call.units.map((unit) => el("span", { className: "chip", text: unit })))]),
      el("td", {}, [actions]),
    ]);
    return row;
  });

  body.replaceChildren(...rows);
  document.getElementById("calls-empty").hidden = calls.length > 0;
  setText("call-count", `${calls.length} active`);
}

function renderUnits(units, self) {
  const body = document.getElementById("units");
  const rows = units.map((unit) =>
    el("tr", { className: unit.serverId === self.serverId ? "mine" : "" }, [
      el("td", { className: "mono", text: unit.callsign }),
      el("td", {}, [
        el("span", { text: unit.name }),
        el("span", { className: "sub muted", text: unit.agency }),
      ]),
      el("td", { className: "muted", text: unit.territory ?? "—" }),
      el("td", {}, [statusPill(unit.status), unit.callId ? el("span", { className: "sub mono muted", text: `#${unit.callId}` }) : null]),
    ])
  );

  body.replaceChildren(...rows);
  setText("unit-count", `${units.length} on duty`);
}

function render() {
  if (!view || !view.self) return;
  renderSelf(view.self);
  renderCalls(view.calls ?? [], view.self);
  renderUnits(view.units ?? [], view.self);
}

function tickClock() {
  const now = new Date();
  document.getElementById("clock").textContent = now.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
}

window.addEventListener("message", (event) => {
  const { type, payload } = event.data ?? {};

  if (type === "open") {
    view = payload;
    root.hidden = false;
    tickClock();
    render();
  } else if (type === "update") {
    view = payload;
    render();
  } else if (type === "close") {
    root.hidden = true;
  }
});

document.addEventListener("click", (event) => {
  const target = event.target.closest("button");
  if (!target || target.disabled) return;

  if (target.id === "close") post("close");
  else if (target.dataset.status) post("setStatus", { status: target.dataset.status });
  else if (target.dataset.action === "attach") post("attach", { callId: target.dataset.call });
  else if (target.dataset.action === "waypoint") post("waypoint", { callId: target.dataset.call });
});

document.addEventListener("keydown", (event) => {
  if (event.key === "Escape" && !root.hidden) post("close");
});

setInterval(tickClock, 15000);
