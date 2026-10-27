const STOLEN = 1;

let view = null;
let selectedId = null;

function plateBadge(plate, state) {
  return el("span", { className: "plate" }, [
    el("span", { className: "plate-state", text: String(state?.name ?? "San Andreas").toUpperCase() }),
    el("span", { className: "plate-number", text: plate }),
  ]);
}

function vehicleRow(vehicle) {
  const stolen = vehicle.status === STOLEN;
  const button = el("button", { className: `vehicle${vehicle.id === selectedId ? " selected" : ""}`, dataset: { vehicle: vehicle.id } }, [
    plateBadge(vehicle.plate, view.stateInfo),
    el("span", {}, [
      el("span", { className: "vehicle-name", text: vehicle.model }),
      el("span", { className: "vehicle-detail", text: `${stolen ? "Reported stolen" : "Registered"} ${formatDate(vehicle.registeredAt)}` }),
    ]),
    el("span", { className: `pill ${stolen ? "bad" : "good"}`, text: stolen ? "Stolen" : "Registered" }),
  ]);
  button.type = "button";
  return button;
}

function renderCurrent(state, current) {
  const registered = current && state.vehicles.some((vehicle) => vehicle.plate === current.plate);
  const full = state.vehicles.length >= state.maxVehicles;

  document.getElementById("current").classList.toggle("idle", !current);
  setText("current-title", current ? `${current.model} · ${current.plate}` : "Not in a vehicle");
  setText("current-detail", !current
    ? "Get in the driver's seat of a vehicle to register it."
    : registered
      ? "Registered to you."
      : full
        ? `You've reached the limit of ${state.maxVehicles} vehicles.`
        : "Not registered to anyone.");

  const register = document.getElementById("register");
  register.hidden = !current || registered;
  register.disabled = full;
}

function renderDetail(active, vehicle) {
  document.getElementById("detail").hidden = !vehicle;
  if (!vehicle) return;

  const stolen = vehicle.status === STOLEN;
  setText("slip-issuer", `${view.stateInfo?.abbreviation ?? "SA"} DMV`);
  setText("slip-plate", vehicle.plate);
  setText("slip-model", vehicle.model);
  setText("slip-owner", `${active.firstName} ${active.lastName}`);
  setText("slip-id", stateId(view, active.id));
  setText("slip-issued", cardDate(vehicle.registeredAt));

  const status = document.getElementById("slip-status");
  status.className = `slip-status ${stolen ? "stolen" : "valid"}`;
  status.textContent = stolen ? "STOLEN" : "VALID";

  const stolenButton = document.getElementById("stolen");
  stolenButton.disabled = false;
  stolenButton.textContent = stolen ? "Mark recovered" : "Report stolen";

  const remove = document.getElementById("remove");
  delete remove.dataset.armed;
  remove.disabled = false;
  remove.textContent = "Remove registration";
}

function render(next) {
  view = next;
  const active = renderCivilianFrame(next);
  if (!active) return;

  const state = next.state;
  if (!state.vehicles.some((vehicle) => vehicle.id === selectedId)) selectedId = state.vehicles[0]?.id ?? null;

  const managed = state.managedBy;
  document.getElementById("current").hidden = Boolean(managed);
  document.getElementById("remove").hidden = Boolean(managed);
  const emptyList = document.getElementById("vehicles-empty");
  emptyList.dataset.text ??= emptyList.textContent;
  emptyList.textContent = managed ? `No vehicles. The vehicles you own in ${managed} show up here.` : emptyList.dataset.text;

  renderCurrent(state, next.currentVehicle);
  setText("count", managed ? `${state.vehicles.length}` : `${state.vehicles.length} / ${state.maxVehicles}`);
  document.getElementById("vehicles").replaceChildren(...state.vehicles.map(vehicleRow));
  document.getElementById("vehicles-empty").hidden = state.vehicles.length > 0;
  renderDetail(active, state.vehicles.find((vehicle) => vehicle.id === selectedId));
}

FivePRS.on("state", render);

document.getElementById("register").addEventListener("click", (event) => {
  event.currentTarget.disabled = true;
  FivePRS.action("register");
});

document.getElementById("vehicles").addEventListener("click", (event) => {
  const target = event.target.closest("[data-vehicle]");
  if (!target) return;
  selectedId = Number(target.dataset.vehicle);
  render(view);
});

document.getElementById("stolen").addEventListener("click", (event) => {
  const vehicle = view?.state?.vehicles.find((entry) => entry.id === selectedId);
  if (!vehicle) return;
  event.currentTarget.disabled = true;
  FivePRS.action("stolen", { vehicleId: vehicle.id, stolen: vehicle.status !== STOLEN });
});

document.getElementById("remove").addEventListener("click", (event) => {
  const button = event.currentTarget;
  if (!selectedId || !confirmButton(button, "Click again to remove")) return;
  button.disabled = true;
  FivePRS.action("remove", { vehicleId: selectedId });
});
