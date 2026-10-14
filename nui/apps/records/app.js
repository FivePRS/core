const search = document.getElementById("search");
const term = document.getElementById("term");
const entryForm = document.getElementById("entry");
const offence = document.getElementById("offence");
const fine = document.getElementById("fine");
const notes = document.getElementById("notes");

const recordDots = ["", "bad", "warn", "bad"];
const licenseStatus = [["Valid", "good"], ["Suspended", "warn"], ["Revoked", "bad"]];

let view = null;
let filled = false;
let searchMode = "name";
let entryType = 0;
let toastTimer = null;
let shownRecordId = null;

function showToast(message) {
  const toast = document.getElementById("toast");
  toast.textContent = message ?? "";
  toast.hidden = !message;
  clearTimeout(toastTimer);
  toastTimer = message ? setTimeout(() => showToast(null), 6000) : null;
}

function button(text, className, dataset) {
  const node = el("button", { className, text, dataset });
  node.type = "button";
  return node;
}

function setSearchMode(mode) {
  searchMode = mode;
  for (const node of search.querySelectorAll("[data-mode]")) node.classList.toggle("active", node.dataset.mode === mode);
  term.placeholder = mode === "plate" ? "Plate" : "First or last name";
  term.maxLength = mode === "plate" ? 8 : 49;
}

function setEntryType(type) {
  entryType = type;
  for (const node of entryForm.querySelectorAll("[data-type]")) node.classList.toggle("active", Number(node.dataset.type) === type);
  document.getElementById("offence-field").hidden = type !== 0 || (view?.offences ?? []).length === 0;
  document.getElementById("fine-field").hidden = type !== 0;
}

function renderOffences() {
  const offences = view?.offences ?? [];
  offence.replaceChildren(
    el("option", { text: "Choose an offence" }),
    ...offences.map((item, index) => {
      const option = el("option", { text: `${item.label} · $${item.fine}` });
      option.value = String(index);
      return option;
    }),
  );
  offence.firstChild.value = "";
}

function renderResults() {
  const results = view.results ?? [];
  const title = document.getElementById("results-title");
  title.hidden = view.results === null || view.results === undefined;
  title.textContent = `${results.length} ${results.length === 1 ? "match" : "matches"} for "${view.query ?? ""}"`;

  document.getElementById("results").replaceChildren(...results.map((person) => {
    const node = el("button", { className: `result${view.record?.character.id === person.id ? " selected" : ""}`, dataset: { person: person.id } }, [
      el("span", { className: "initials", text: `${person.firstName.charAt(0)}${person.lastName.charAt(0)}` }),
      el("span", { className: "result-text" }, [
        el("span", { className: "result-name", text: `${person.firstName} ${person.lastName}` }),
        el("span", { className: "result-dob", text: birthDate(person.dateOfBirth) }),
      ]),
      person.hasActiveWarrant ? el("span", { className: "flag", text: "WARRANT" }) : null,
    ]);
    node.type = "button";
    return node;
  }));
  document.getElementById("results-empty").hidden = !(view.results && results.length === 0);
}

function licenseActions(license) {
  if (license.status === null || license.status === undefined) return [button("Issue", "button small", { license: license.type, status: 0 })];
  if (license.status === 0) return [button("Suspend", "button small", { license: license.type, status: 1 }), button("Revoke", "button small danger", { license: license.type, status: 2 })];
  return [button("Reinstate", "button small", { license: license.type, status: 0 })];
}

function renderLicenses(record) {
  document.getElementById("licenses").replaceChildren(...record.licenses.map((license) => {
    const held = license.status !== null && license.status !== undefined;
    const [label, tone] = held ? licenseStatus[license.status] : ["Not held", ""];
    return el("div", { className: "row" }, [
      el("span", { className: "row-main" }, [
        el("span", { className: "row-title", text: license.name }),
        el("span", { className: `pill ${tone}`, text: label }),
        held ? el("span", { className: "row-sub", text: license.number }) : null,
      ]),
      el("span", { className: "row-actions" }, licenseActions(license)),
    ]);
  }));
}

function renderVehicles(record) {
  document.getElementById("vehicles").replaceChildren(...record.vehicles.map((vehicle) => {
    const stolen = vehicle.status === 1;
    return el("div", { className: `row${vehicle.plate === record.matchedPlate ? " highlight" : ""}` }, [
      el("span", { className: "row-main" }, [
        el("span", { className: "row-title mono", text: vehicle.plate }),
        el("span", { text: vehicle.model }),
        el("span", { className: `pill ${stolen ? "bad" : "good"}`, text: stolen ? "Stolen" : "Registered" }),
      ]),
      el("span", { className: "row-actions" }, [
        button(stolen ? "Mark recovered" : "Mark stolen", "button small", { vehicle: vehicle.id, stolen: String(!stolen) }),
      ]),
    ]);
  }));
  document.getElementById("vehicles-empty").hidden = record.vehicles.length > 0;
}

function renderHistory(record) {
  document.getElementById("history").replaceChildren(...record.records.map((entry) => {
    const activeWarrant = entry.type === 3 && entry.active;
    const meta = [formatDate(entry.createdAt), entry.officerCallsign, entry.fine > 0 ? `$${entry.fine}` : null].filter(Boolean).join(" · ");
    return el("div", { className: "entry" }, [
      el("span", { className: `entry-dot ${recordDots[entry.type] ?? ""}` }),
      el("div", {}, [
        el("div", { className: "entry-head" }, [
          el("span", { className: "entry-type", text: activeWarrant ? "Active warrant" : recordTypes[entry.type] }),
          el("span", { className: "entry-meta", text: meta }),
        ]),
        el("p", { className: "entry-text", text: entry.description }),
        entry.resolution ? el("p", { className: "entry-meta", text: entry.resolution }) : null,
        activeWarrant ? el("div", { className: "entry-actions" }, [
          button("Mark served", "button small", { resolve: entry.id, served: "true" }),
          button("Clear", "button small", { resolve: entry.id, served: "false" }),
        ]) : null,
      ]),
    ]);
  }));
  document.getElementById("history-empty").hidden = record.records.length > 0;
}

function renderRecord(record) {
  const person = record.character;
  const abbreviation = view.stateInfo?.abbreviation ?? "SA";
  setText("name", `${person.lastName.toUpperCase()}, ${person.firstName.toUpperCase()}`);
  setText("details", `${abbreviation}-${10000 + person.id} · DOB ${birthDate(person.dateOfBirth)} (${age(person.dateOfBirth)}) · ${String(person.gender).charAt(0)}`);
  document.getElementById("photo").replaceWith(Object.assign(photoFrame(person.photo, "photo board person-photo"), { id: "photo" }));

  const plate = document.getElementById("plate-match");
  plate.hidden = !record.matchedPlate;
  plate.textContent = record.matchedPlate ? `Registered owner of ${record.matchedPlate}` : "";

  const warrant = record.records.find((entry) => entry.type === 3 && entry.active);
  document.getElementById("warrant").hidden = !warrant;
  if (warrant) setText("warrant-detail", `${warrant.description} · ${warrant.officerCallsign || "on file"}, ${formatDate(warrant.createdAt)}`);

  const driver = record.licenses.find((license) => license.type === "driver");
  const flags = [];
  if (driver) {
    const held = driver.status !== null && driver.status !== undefined;
    flags.push(el("span", { className: `pill ${held ? licenseStatus[driver.status][1] : "bad"}`, text: held ? `DL ${licenseStatus[driver.status][0].toLowerCase()}${driver.class ? ` · Class ${driver.class}` : ""}` : "No driver's license" }));
  }
  for (const license of record.licenses.filter((item) => item.type !== "driver" && item.status > 0)) {
    flags.push(el("span", { className: `pill ${licenseStatus[license.status][1]}`, text: `${license.name} ${licenseStatus[license.status][0].toLowerCase()}` }));
  }
  if (record.vehicles.length) flags.push(el("span", { className: "pill", text: `${record.vehicles.length} vehicle${record.vehicles.length === 1 ? "" : "s"}` }));
  if (record.vehicles.some((vehicle) => vehicle.status === 1)) flags.push(el("span", { className: "pill bad", text: "Stolen vehicle" }));
  document.getElementById("flags").replaceChildren(...flags);

  renderLicenses(record);
  renderVehicles(record);
  renderHistory(record);

  if (shownRecordId !== person.id) {
    shownRecordId = person.id;
    entryForm.reset();
    setEntryType(0);
  }
}

function render(next) {
  view = next;

  if (!filled) {
    filled = true;
    fine.max = next.maxFine;
    notes.maxLength = next.maxDescription;
    renderOffences();
    setEntryType(0);
  }

  renderResults();

  const record = next.record;
  document.getElementById("placeholder").hidden = Boolean(record);
  document.getElementById("record").hidden = !record;
  document.getElementById("submit").disabled = !record;
  document.getElementById("entry-hint").hidden = Boolean(record);
  if (record) renderRecord(record);
  else shownRecordId = null;
}

FivePRS.on("state", render);
FivePRS.on("error", (message) => {
  showToast(message);
  if (view?.record) document.getElementById("submit").disabled = false;
});

search.addEventListener("submit", (event) => {
  event.preventDefault();
  const value = term.value.trim();
  if (!value) return;
  showToast(null);
  if (searchMode === "plate") FivePRS.action("searchPlate", { plate: value });
  else FivePRS.action("searchName", { term: value });
});

offence.addEventListener("change", () => {
  const item = (view?.offences ?? [])[Number(offence.value)];
  if (!item) return;
  fine.value = String(Math.min(item.fine, view.maxFine));
  if (!notes.value.trim()) notes.value = item.label;
});

entryForm.addEventListener("submit", (event) => {
  event.preventDefault();
  const record = view?.record;
  if (!record) return;

  document.getElementById("submit").disabled = true;
  showToast(null);
  shownRecordId = null;
  FivePRS.action("issue", {
    characterId: record.character.id,
    type: entryType,
    description: notes.value.trim(),
    fine: entryType === 0 ? Number(fine.value || 0) : 0,
  });
});

document.addEventListener("click", (event) => {
  const target = event.target.closest("button");
  if (!target || target.disabled || target.type === "submit") return;
  const record = view?.record;

  if (target.dataset.mode) {
    setSearchMode(target.dataset.mode);
  } else if (target.dataset.type) {
    setEntryType(Number(target.dataset.type));
  } else if (target.dataset.person) {
    FivePRS.action("open", { characterId: Number(target.dataset.person) });
  } else if (target.id === "back") {
    FivePRS.action("back");
  } else if (target.dataset.license && record) {
    target.disabled = true;
    FivePRS.action("license", { characterId: record.character.id, type: target.dataset.license, status: Number(target.dataset.status) });
  } else if (target.dataset.vehicle) {
    target.disabled = true;
    FivePRS.action("vehicle", { vehicleId: Number(target.dataset.vehicle), stolen: target.dataset.stolen === "true" });
  } else if (target.dataset.resolve) {
    target.disabled = true;
    FivePRS.action("resolve", { recordId: Number(target.dataset.resolve), served: target.dataset.served === "true" });
  }
});

setSearchMode("name");
