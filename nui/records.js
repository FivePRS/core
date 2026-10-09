const recordsTab = document.getElementById("tab-records");
const recordSearch = document.getElementById("rec-search");
const recordForm = document.getElementById("rec-form");
const recordTerm = document.getElementById("rec-term");

const recordTypes = ["Citation", "Arrest", "Warning", "Warrant"];
const recordTones = ["enroute", "revoked", "busy", "revoked"];
const licenseLabels = ["Valid", "Suspended", "Revoked"];
const licenseTones = ["available", "enroute", "revoked"];

let recordsView = null;
let searchMode = "name";
let recordsErrorTimer = null;
let shownRecordId = null;

function showRecordsError(message) {
  const error = document.getElementById("rec-error");
  error.textContent = message ?? "";
  error.hidden = !message;
  if (recordsErrorTimer !== null) clearTimeout(recordsErrorTimer);
  recordsErrorTimer = message ? setTimeout(() => showRecordsError(null), 6000) : null;
}

function setSearchMode(mode) {
  searchMode = mode;
  for (const button of recordSearch.querySelectorAll("[data-mode]")) button.classList.toggle("active", button.dataset.mode === mode);
  recordTerm.placeholder = mode === "plate" ? "Plate" : "First or last name";
  recordTerm.maxLength = mode === "plate" ? 8 : 49;
}

function formatDate(value) {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "" : date.toLocaleDateString([], { year: "numeric", month: "short", day: "numeric" });
}

function renderResults(view) {
  const results = view.results ?? [];
  const title = document.getElementById("rec-results-title");
  title.hidden = view.results === null || view.results === undefined;
  title.textContent = `Results for "${view.query ?? ""}"`;

  document.getElementById("rec-results").replaceChildren(
    ...results.map((person) =>
      el("button", {
        className: `option civ-character${view.record?.character.id === person.id ? " selected" : ""}`,
        dataset: { person: person.id },
      }, [
        el("span", { className: "option-name", text: `${person.firstName} ${person.lastName}` }),
        el("span", { className: "option-detail", text: person.dateOfBirth }),
        person.hasActiveWarrant ? el("span", { className: "status status-revoked rec-result-flag", text: "Warrant" }) : null,
      ])
    )
  );
  document.getElementById("rec-results-empty").hidden = !(view.results && results.length === 0);
}

function licenseActions(license) {
  const actions = [];
  const add = (label, status, tone) =>
    actions.push(el("button", { className: `button small${tone ? ` ${tone}` : ""}`, text: label, dataset: { license: license.type, status } }));

  if (license.status === null || license.status === undefined) add("Issue", 0);
  else if (license.status === 0) {
    add("Suspend", 1);
    add("Revoke", 2, "danger");
  } else add("Reinstate", 0);

  return el("div", { className: "actions" }, actions);
}

function renderRecordDetail(record) {
  const person = record.character;
  setText("rec-name", `${person.firstName} ${person.lastName}`);
  setText("rec-details", `Born ${person.dateOfBirth} · ${age(person.dateOfBirth)} years old · ${person.gender}`);
  const plate = document.getElementById("rec-plate");
  plate.hidden = !record.matchedPlate;
  plate.textContent = record.matchedPlate ? `Registered owner of ${record.matchedPlate}` : "";
  document.getElementById("rec-warrant").hidden = !person.hasActiveWarrant;

  document.getElementById("rec-licenses").replaceChildren(
    ...record.licenses.map((license) => {
      const status = license.status === null || license.status === undefined
        ? el("span", { className: "muted civ-note", text: "None" })
        : el("span", { className: `status status-${licenseTones[license.status]}`, text: licenseLabels[license.status] });
      return el("div", { className: "civ-license rec-license" }, [
        el("div", {}, [el("span", { className: "option-name", text: license.name }), status]),
        licenseActions(license),
      ]);
    })
  );

  document.getElementById("rec-vehicles").replaceChildren(
    ...record.vehicles.map((vehicle) => {
      const stolen = vehicle.status === 1;
      return el("tr", { className: vehicle.plate === record.matchedPlate ? "mine" : "" }, [
        el("td", { className: "mono", text: vehicle.plate }),
        el("td", { text: vehicle.model }),
        el("td", {}, [stolen ? el("span", { className: "status status-revoked", text: "Stolen" }) : el("span", { className: "status status-available", text: "Registered" })]),
        el("td", {}, [
          el("div", { className: "actions" }, [
            el("button", {
              className: "button small",
              text: stolen ? "Mark recovered" : "Mark stolen",
              dataset: { vehicle: vehicle.id, stolen: String(!stolen) },
            }),
          ]),
        ]),
      ]);
    })
  );
  document.getElementById("rec-vehicles-empty").hidden = record.vehicles.length > 0;

  document.getElementById("rec-history").replaceChildren(
    ...record.records.map((entry) => {
      const meta = [formatDate(entry.createdAt), entry.officerCallsign, entry.fine > 0 ? `$${entry.fine}` : null].filter(Boolean).join(" · ");
      const warrantActions = entry.type === 3 && entry.active
        ? el("div", { className: "actions" }, [
            el("button", { className: "button small", text: "Served", dataset: { resolve: entry.id, served: "true" } }),
            el("button", { className: "button small", text: "Clear", dataset: { resolve: entry.id, served: "false" } }),
          ])
        : null;

      return el("div", { className: "rec-entry" }, [
        el("div", { className: "rec-entry-head" }, [
          el("span", { className: `status status-${recordTones[entry.type]}`, text: entry.type === 3 && entry.active ? "Active warrant" : recordTypes[entry.type] }),
          el("span", { className: "muted rec-entry-meta", text: meta }),
          warrantActions,
        ]),
        el("p", { className: "rec-entry-text", text: entry.description }),
        entry.resolution ? el("p", { className: "muted rec-entry-meta", text: entry.resolution }) : null,
      ]);
    })
  );
  document.getElementById("rec-history-empty").hidden = record.records.length > 0;

  if (shownRecordId !== person.id) {
    shownRecordId = person.id;
    recordForm.reset();
    updateFineField();
  }
  recordForm.querySelector("button[type=submit]").disabled = false;
}

function updateFineField() {
  const isCitation = recordForm.elements.type.value === "0";
  document.getElementById("rec-fine-field").hidden = !isCitation;
}

function renderRecords(view, reset) {
  recordsView = view;
  if (!view) return;

  if (reset) {
    showRecordsError(null);
    recordForm.elements.fine.max = view.maxFine;
    recordForm.elements.description.maxLength = view.maxDescription;
  }

  renderResults(view);

  const record = view.record;
  document.getElementById("rec-placeholder").hidden = Boolean(record);
  document.getElementById("rec-detail").hidden = !record;
  if (record) renderRecordDetail(record);
  else shownRecordId = null;
}

recordSearch.addEventListener("submit", (event) => {
  event.preventDefault();
  const term = recordTerm.value.trim();
  if (!term) return;
  showRecordsError(null);
  post(searchMode === "plate" ? "recSearchPlate" : "recSearchName", searchMode === "plate" ? { plate: term } : { term });
});

recordForm.elements.type.addEventListener("change", updateFineField);

recordForm.addEventListener("submit", (event) => {
  event.preventDefault();
  const record = recordsView?.record;
  if (!record) return;

  const data = new FormData(recordForm);
  recordForm.querySelector("button[type=submit]").disabled = true;
  showRecordsError(null);
  shownRecordId = null;
  post("recIssue", {
    characterId: record.character.id,
    type: Number(data.get("type")),
    description: String(data.get("description") ?? "").trim(),
    fine: Number(data.get("fine") ?? 0),
  });
});

recordsTab.addEventListener("click", (event) => {
  const target = event.target.closest("button");
  if (!target || target.disabled) return;
  const record = recordsView?.record;

  if (target.dataset.mode) {
    setSearchMode(target.dataset.mode);
  } else if (target.dataset.person) {
    post("recOpen", { characterId: Number(target.dataset.person) });
  } else if (target.dataset.action === "back") {
    post("recBack");
  } else if (target.dataset.license && record) {
    target.disabled = true;
    post("recSetLicense", { characterId: record.character.id, type: target.dataset.license, status: Number(target.dataset.status) });
  } else if (target.dataset.vehicle) {
    target.disabled = true;
    post("recFlagVehicle", { vehicleId: Number(target.dataset.vehicle), stolen: target.dataset.stolen === "true" });
  } else if (target.dataset.resolve) {
    target.disabled = true;
    post("recResolve", { recordId: Number(target.dataset.resolve), served: target.dataset.served === "true" });
  }
});

setSearchMode("name");
