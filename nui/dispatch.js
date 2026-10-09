const statusLabels = {
  Available: "Available",
  EnRoute: "En route",
  OnScene: "On scene",
  Busy: "Busy",
};

const dispatchTab = document.getElementById("tab-dispatch");
let offerDeadline = 0;
let offerTotalMs = 0;
let offerTimer = null;

function statusPill(status) {
  return el("span", { className: `status status-${status.toLowerCase()}`, text: statusLabels[status] ?? status });
}

function tickOffer() {
  const remaining = Math.max(0, offerDeadline - Date.now());
  const fraction = offerTotalMs > 0 ? remaining / offerTotalMs : 0;
  document.getElementById("offer-bar").style.width = `${(fraction * 100).toFixed(1)}%`;
  document.getElementById("offer-countdown").textContent = `${Math.ceil(remaining / 1000)}s`;
  if (remaining <= 0) stopOfferTimer();
}

function stopOfferTimer() {
  if (offerTimer !== null) clearInterval(offerTimer);
  offerTimer = null;
}

function renderCallCards(offer, activeCall) {
  const offerCard = document.getElementById("offer");
  offerCard.hidden = !offer;
  stopOfferTimer();

  if (offer) {
    setText("offer-title", `#${offer.id} ${offer.name} · Code ${offer.code}`);
    setText("offer-description", offer.description || "");
    offerTotalMs = offer.windowMs || offer.expiresInMs;
    offerDeadline = Date.now() + offer.expiresInMs;
    for (const button of offerCard.querySelectorAll("button")) button.disabled = false;
    tickOffer();
    offerTimer = setInterval(tickOffer, 250);
  }

  const activeCard = document.getElementById("active-call");
  activeCard.hidden = !activeCall;
  if (activeCall) {
    setText("active-title", `#${activeCall.id} ${activeCall.name}`);
    activeCard.querySelector("button").disabled = false;
  }
}

function renderSelf(self, calls) {
  setText("self-callsign", self.callsign);
  setText("self-name", self.name);
  setText("self-agency", self.agency);
  setText("self-territory", self.territory ?? "Unincorporated");
  setText("self-call", self.callId ? `#${self.callId}` : "None");
  document.getElementById("self-status").replaceChildren(statusPill(self.status));

  const leadsCall = calls.some((call) => call.isPrimary);
  for (const button of dispatchTab.querySelectorAll("[data-status]")) {
    button.classList.toggle("active", button.dataset.status === self.status);
    button.disabled = leadsCall;
  }
  document.getElementById("status-hint").textContent = leadsCall
    ? "End your active call to change status."
    : "En route and on scene update automatically from your position.";
}

function renderCalls(calls, self) {
  const rows = calls.map((call) => {
    const mine = self.callId === call.id;

    const actions = el("div", { className: "actions" }, [
      call.canClear
        ? el("button", { className: "button small danger", text: "Clear", dataset: { action: "clear", call: call.id } })
        : null,
      call.hasLocation
        ? el("button", { className: "button small", text: "Waypoint", dataset: { action: "waypoint", call: call.id } })
        : null,
      mine
        ? null
        : el("button", {
            className: "button small",
            text: "Attach",
            dataset: { action: "attach", call: call.id },
            disabled: Boolean(self.callId),
          }),
    ]);

    return el("tr", { className: mine ? "mine" : "" }, [
      el("td", { className: "mono", text: `#${call.id}` }),
      el("td", {}, [
        el("span", { text: call.name }),
        el("span", { className: `sub code-${call.code}`, text: `Code ${call.code}` }),
        call.isEmergency ? el("span", { className: "sub muted em-call-text", text: `${call.caller}: ${call.description}` }) : null,
      ]),
      el("td", { className: "muted", text: call.territory ?? "—" }),
      el("td", {}, [el("div", { className: "chips" }, call.units.map((unit) => el("span", { className: "chip", text: unit })))]),
      el("td", {}, [actions]),
    ]);
  });

  document.getElementById("calls").replaceChildren(...rows);
  document.getElementById("calls-empty").hidden = calls.length > 0;
  setText("call-count", `${calls.length} active`);
}

function renderUnits(units, self) {
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

  document.getElementById("units").replaceChildren(...rows);
  setText("unit-count", `${units.length} on duty`);
}

function renderDispatch(view) {
  if (!view) {
    stopOfferTimer();
    return;
  }

  const calls = view.calls ?? [];
  renderCallCards(view.offer, view.activeCall);
  renderSelf(view.self, calls);
  renderCalls(calls, view.self);
  renderUnits(view.units ?? [], view.self);
}

dispatchTab.addEventListener("click", (event) => {
  const target = event.target.closest("button");
  if (!target || target.disabled) return;

  if (target.dataset.status) {
    post("setStatus", { status: target.dataset.status });
  } else if (target.dataset.action === "attach") {
    post("attach", { callId: target.dataset.call });
  } else if (target.dataset.action === "clear") {
    target.disabled = true;
    post("callClear", { callId: target.dataset.call });
  } else if (target.dataset.action === "waypoint") {
    post("waypoint", { callId: target.dataset.call });
  } else if (target.dataset.action === "offer-accept" || target.dataset.action === "offer-decline") {
    for (const button of document.querySelectorAll("#offer button")) button.disabled = true;
    post(target.dataset.action === "offer-accept" ? "offerAccept" : "offerDecline");
  } else if (target.dataset.action === "end-call") {
    target.disabled = true;
    post("endCall");
  }
});
