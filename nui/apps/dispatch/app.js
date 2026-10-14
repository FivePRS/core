const statusLabels = {
  Available: "Available",
  EnRoute: "En route",
  OnScene: "On scene",
  Busy: "Busy",
};

const unitGroups = [
  { title: "On calls", match: (unit) => unit.callId },
  { title: "Available", match: (unit) => !unit.callId && unit.status === "Available" },
  { title: "Busy", match: (unit) => !unit.callId && unit.status !== "Available" },
];

let view = null;
let offerTimer = null;
let shownOffer = null;

function button(text, className, dataset, disabled = false) {
  const node = el("button", { className, text, dataset, disabled });
  node.type = "button";
  return node;
}

function formatDistance(meters) {
  if (meters === null || meters === undefined) return "";
  const miles = meters / 1609.34;
  return miles < 0.1 ? "Nearby" : `${miles.toFixed(1)} mi`;
}

function codeTag(id, code) {
  const node = document.getElementById(id);
  node.className = `code c${code}`;
  node.textContent = `CODE ${code}`;
}

function tickOffer() {
  const offer = view?.offer;
  if (!offer) return;
  const remaining = Math.max(0, offer.expiresAt - Date.now());
  document.getElementById("offer-bar").style.width = `${((remaining / Math.max(1, offer.windowMs)) * 100).toFixed(1)}%`;
  setText("offer-countdown", `${Math.ceil(remaining / 1000)}s`);
}

function renderOffer(offer) {
  document.getElementById("offer").hidden = !offer;
  if (!offer) {
    clearInterval(offerTimer);
    offerTimer = null;
    shownOffer = null;
    return;
  }

  if (shownOffer === offer.id) return;
  shownOffer = offer.id;
  codeTag("offer-code", offer.code);
  setText("offer-title", `#${offer.id} ${offer.name}`);
  setText("offer-description", [offer.description, formatDistance(offer.distanceM)].filter(Boolean).join(" · "));
  for (const node of document.querySelectorAll("#offer button")) node.disabled = false;
  clearInterval(offerTimer);
  tickOffer();
  offerTimer = setInterval(tickOffer, 250);
}

function renderActive(active, self) {
  document.getElementById("active").hidden = !active;
  if (!active) return;

  codeTag("active-code", active.code);
  setText("active-tag", `Your call · #${active.id}`);
  setText("active-title", active.name);
  setText("active-description", active.description || "");
  document.getElementById("active-units").replaceChildren(
    ...(active.units ?? []).map((callsign) => el("span", { className: "chip", text: callsign === self.callsign ? `${callsign} · you` : callsign }))
  );
  for (const node of document.querySelectorAll("#active button")) node.disabled = false;
}

function renderSelf(self, calls) {
  setDepartmentIcon("me-icon", self.department);
  setText("me-callsign", self.callsign);
  setText("me-detail", `${self.name} · ${self.agency} · ${self.territory ?? "Unincorporated"}`);

  const leadsCall = calls.some((call) => call.isPrimary);
  for (const node of document.querySelectorAll("[data-status]")) {
    node.classList.toggle("active", node.dataset.status === self.status);
    node.disabled = leadsCall;
  }
  setText("status-hint", leadsCall ? "End your call to change status" : statusLabels[self.status] ?? self.status);
}

function renderCalls(calls, self) {
  document.getElementById("calls").replaceChildren(...calls.map((call) => {
    const mine = self.callId === call.id;
    const detail = call.isEmergency
      ? `“${call.description}” · ${call.caller}`
      : [call.territory, formatDistance(call.distanceM)].filter(Boolean).join(" · ");

    return el("div", { className: `call${mine ? " mine" : ""}` }, [
      el("span", { className: "call-id", text: `#${call.id}` }),
      el("span", { className: "call-body" }, [
        el("span", {}, [
          el("span", { className: "call-title", text: call.name }),
          el("span", { className: `call-code c${call.code}`, text: `CODE ${call.code}` }),
        ]),
        el("span", { className: "call-detail", text: detail }),
      ]),
      el("span", { className: "call-units", text: call.units.length ? call.units.join(", ") : "No units" }),
      el("span", { className: "call-actions" }, [
        call.canClear ? button("Clear", "button small danger", { action: "clear", call: call.id }) : null,
        call.hasLocation ? button("Waypoint", "button small", { action: "waypoint", call: call.id }) : null,
        mine ? null : button("Attach", "button small", { action: "attach", call: call.id }, Boolean(self.callId)),
      ]),
    ]);
  }));

  document.getElementById("calls-empty").hidden = calls.length > 0;
  setText("call-count", `${calls.length} open`);
}

function renderUnits(units, self) {
  const groups = unitGroups
    .map((group) => ({ title: group.title, units: units.filter(group.match) }))
    .filter((group) => group.units.length > 0);

  document.getElementById("units").replaceChildren(...groups.flatMap((group) => [
    el("p", { className: "eyebrow group-title", text: group.title }),
    ...group.units.map((unit) => el("div", { className: `unit${unit.serverId === self.serverId ? " mine" : ""}` }, [
      el("span", { className: `dot ${unit.status.toLowerCase()}` }),
      el("span", { className: "unit-callsign", text: unit.callsign }),
      el("span", { className: "unit-name", text: unit.name }),
      el("span", { className: "unit-extra", text: unit.callId ? `#${unit.callId}` : unit.territory ?? "" }),
    ])),
  ]));
  setText("unit-count", `${units.length} on duty`);
}

function render(next) {
  view = next;
  setDepartmentIcons(next.icons);
  const calls = next.calls ?? [];
  renderSelf(next.self, calls);
  renderOffer(next.offer);
  renderActive(next.activeCall, next.self);
  renderCalls(calls, next.self);
  renderUnits(next.units ?? [], next.self);
}

FivePRS.on("state", render);

document.addEventListener("click", (event) => {
  const target = event.target.closest("button");
  if (!target || target.disabled) return;
  const action = target.dataset.action;

  if (target.dataset.status) {
    FivePRS.action("status", { status: target.dataset.status });
  } else if (action === "attach" || action === "waypoint") {
    FivePRS.action(action, { callId: target.dataset.call });
  } else if (action === "clear") {
    target.disabled = true;
    FivePRS.action("clear", { callId: target.dataset.call });
  } else if (action === "offer-accept" || action === "offer-decline") {
    for (const node of document.querySelectorAll("#offer button")) node.disabled = true;
    FivePRS.action(action === "offer-accept" ? "accept" : "decline");
  } else if (action === "end-call") {
    target.disabled = true;
    FivePRS.action("end");
  } else if (action === "backup") {
    target.disabled = true;
    FivePRS.action("backup", { code: 3 });
  }
});
