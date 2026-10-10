const civilianTab = document.getElementById("tab-civilian");
const characterForm = document.getElementById("civ-form");

const licenseStatuses = ["Valid", "Suspended", "Revoked"];
const vehicleStatuses = ["Valid", "Stolen"];

let civilianView = null;
let pendingCharacterCount = null;
let civilianErrorTimer = null;

function pill(label, tone) {
  return el("span", { className: `status status-${tone}`, text: label });
}

function showCivilianError(message) {
  const error = document.getElementById("civ-error");
  error.textContent = message ?? "";
  error.hidden = !message;
  if (civilianErrorTimer !== null) clearTimeout(civilianErrorTimer);
  civilianErrorTimer = message ? setTimeout(() => showCivilianError(null), 6000) : null;
  if (message) pendingCharacterCount = null;
}

function age(dateOfBirth) {
  const born = new Date(`${dateOfBirth}T00:00:00`);
  const now = new Date();
  let years = now.getFullYear() - born.getFullYear();
  if (now.getMonth() < born.getMonth() || (now.getMonth() === born.getMonth() && now.getDate() < born.getDate())) years -= 1;
  return years;
}

function confirmButton(button, label) {
  if (button.dataset.armed === "true") return true;
  button.dataset.armed = "true";
  button.dataset.label = button.textContent;
  button.textContent = label;
  return false;
}

function toggleCharacterForm(open) {
  characterForm.hidden = !open;
  document.getElementById("civ-new").hidden = open;
  if (open) {
    characterForm.reset();
    characterForm.elements.firstName.focus();
  }
}

function renderCharacters(state) {
  const items = state.characters.map((character) =>
    el("button", {
      className: `option civ-character${character.id === state.activeCharacterId ? " selected" : ""}`,
      dataset: { character: character.id },
    }, [
      el("span", { className: "option-name", text: `${character.firstName} ${character.lastName}` }),
      el("span", { className: "option-detail", text: character.dateOfBirth }),
    ])
  );

  document.getElementById("civ-characters").replaceChildren(...items);
  setText("civ-character-count", `${state.characters.length} / ${state.maxCharacters}`);

  const atLimit = state.characters.length >= state.maxCharacters;
  const newButton = document.getElementById("civ-new");
  newButton.disabled = atLimit;
  newButton.textContent = atLimit ? "Character limit reached" : "New character";
}

function renderLicenses(licenses) {
  const rows = licenses.map((license) => {
    const status = license.status === null || license.status === undefined ? null : licenseStatuses[license.status];
    let action;
    if (status) action = pill(status, status === "Valid" ? "available" : status === "Suspended" ? "enroute" : "revoked");
    else if (license.selfService) action = el("button", { className: "button small", text: "Apply", dataset: { license: license.type } });
    else action = el("span", { className: "muted civ-note", text: "Issued by law enforcement" });

    return el("div", { className: "civ-license" }, [el("span", { text: license.name }), action]);
  });

  document.getElementById("civ-licenses").replaceChildren(...rows);
}

function renderVehicles(state, currentVehicle) {
  const rows = state.vehicles.map((vehicle) => {
    const stolen = vehicleStatuses[vehicle.status] === "Stolen";
    return el("tr", {}, [
      el("td", { className: "mono", text: vehicle.plate }),
      el("td", { text: vehicle.model }),
      el("td", {}, [stolen ? pill("Stolen", "revoked") : pill("Registered", "available")]),
      el("td", {}, [
        el("div", { className: "actions" }, [
          el("button", {
            className: "button small",
            text: stolen ? "Mark recovered" : "Report stolen",
            dataset: { action: "stolen", vehicle: vehicle.id, stolen: String(!stolen) },
          }),
          el("button", { className: "button small danger", text: "Remove", dataset: { action: "remove-vehicle", vehicle: vehicle.id } }),
        ]),
      ]),
    ]);
  });

  document.getElementById("civ-vehicles").replaceChildren(...rows);
  document.getElementById("civ-vehicles-empty").hidden = state.vehicles.length > 0;
  setText("civ-vehicle-count", `${state.vehicles.length} / ${state.maxVehicles}`);

  const registered = currentVehicle && state.vehicles.some((vehicle) => vehicle.plate === currentVehicle.plate);
  const register = document.getElementById("civ-register");
  register.disabled = !currentVehicle || registered || state.vehicles.length >= state.maxVehicles;
  setText(
    "civ-current-vehicle",
    !currentVehicle
      ? "Get in the driver's seat of a vehicle to register it."
      : registered
        ? `${currentVehicle.model} (${currentVehicle.plate}) is registered to you.`
        : `Current vehicle: ${currentVehicle.model} (${currentVehicle.plate})`
  );
}

function renderOwnRecords(records) {
  document.getElementById("civ-records").replaceChildren(
    ...records.map((entry) => {
      const activeWarrant = entry.type === 3 && entry.active;
      const meta = [formatDate(entry.createdAt), entry.officerCallsign, entry.fine > 0 ? `$${entry.fine}` : null].filter(Boolean).join(" · ");
      return el("div", { className: "rec-entry" }, [
        el("div", { className: "rec-entry-head" }, [
          el("span", { className: `status status-${recordTones[entry.type]}`, text: activeWarrant ? "Active warrant" : recordTypes[entry.type] }),
          el("span", { className: "muted rec-entry-meta", text: meta }),
        ]),
        el("p", { className: "rec-entry-text", text: entry.description }),
        entry.resolution ? el("p", { className: "muted rec-entry-meta", text: entry.resolution }) : null,
      ]);
    })
  );
  document.getElementById("civ-records-empty").hidden = records.length > 0;
}

function renderCivilian(view, reset) {
  civilianView = view;
  setDepartmentIcon("civ-icon", "civilian");
  if (reset) {
    toggleCharacterForm(false);
    showCivilianError(null);
    pendingCharacterCount = null;
  }

  const genderSelect = characterForm.elements.gender;
  if (genderSelect.options.length === 0) {
    genderSelect.replaceChildren(...view.genders.map((gender) => el("option", { text: gender })));
    characterForm.elements.firstName.maxLength = view.maxNameLength;
    characterForm.elements.lastName.maxLength = view.maxNameLength;
  }

  const state = view.state;
  document.getElementById("civ-loading").hidden = Boolean(state);
  if (!state) {
    document.getElementById("civ-empty").hidden = true;
    document.getElementById("civ-profile").hidden = true;
    return;
  }

  if (pendingCharacterCount !== null && state.characters.length > pendingCharacterCount) {
    pendingCharacterCount = null;
    toggleCharacterForm(false);
  }

  renderCharacters(state);

  const active = state.characters.find((character) => character.id === state.activeCharacterId);
  document.getElementById("civ-empty").hidden = Boolean(active);
  document.getElementById("civ-profile").hidden = !active;
  if (!active) return;

  setText("civ-name", `${active.firstName} ${active.lastName}`);
  setText("civ-details", `Born ${active.dateOfBirth} · ${age(active.dateOfBirth)} years old · ${active.gender}`);
  const appearanceButton = document.getElementById("civ-appearance");
  appearanceButton.disabled = Boolean(view.onDuty);
  appearanceButton.title = view.onDuty ? "Go off duty to change your appearance" : "";
  const deleteButton = document.getElementById("civ-delete");
  delete deleteButton.dataset.armed;
  deleteButton.textContent = "Delete character";

  renderLicenses(state.licenses);
  renderVehicles(state, view.currentVehicle);
  renderOwnRecords(state.records ?? []);
}

characterForm.addEventListener("submit", (event) => {
  event.preventDefault();
  const data = new FormData(characterForm);
  showCivilianError(null);
  pendingCharacterCount = civilianView?.state?.characters.length ?? 0;
  post("civCreateCharacter", {
    firstName: String(data.get("firstName") ?? "").trim(),
    lastName: String(data.get("lastName") ?? "").trim(),
    dateOfBirth: String(data.get("dateOfBirth") ?? ""),
    gender: String(data.get("gender") ?? ""),
  });
});

civilianTab.addEventListener("click", (event) => {
  const target = event.target.closest("button");
  if (!target || target.disabled) return;

  if (target.id === "civ-new") {
    toggleCharacterForm(true);
  } else if (target.dataset.action === "cancel-character") {
    toggleCharacterForm(false);
  } else if (target.dataset.character) {
    post("civSelectCharacter", { characterId: Number(target.dataset.character) });
  } else if (target.id === "civ-appearance") {
    post("civAppearance");
  } else if (target.id === "civ-delete") {
    if (confirmButton(target, "Click again to delete")) {
      target.disabled = true;
      post("civDeleteCharacter", { characterId: civilianView?.state?.activeCharacterId });
    }
  } else if (target.dataset.license) {
    target.disabled = true;
    post("civApplyLicense", { type: target.dataset.license });
  } else if (target.id === "civ-register") {
    target.disabled = true;
    post("civRegisterVehicle");
  } else if (target.dataset.action === "stolen") {
    target.disabled = true;
    post("civSetVehicleStolen", { vehicleId: Number(target.dataset.vehicle), stolen: target.dataset.stolen === "true" });
  } else if (target.dataset.action === "remove-vehicle") {
    if (confirmButton(target, "Confirm")) {
      target.disabled = true;
      post("civRemoveVehicle", { vehicleId: Number(target.dataset.vehicle) });
    }
  }
});
