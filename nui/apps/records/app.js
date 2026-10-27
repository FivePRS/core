const search = document.getElementById("search");
const term = document.getElementById("term");
const entryForm = document.getElementById("entry");
const offence = document.getElementById("offence");
const fine = document.getElementById("fine");
const notes = document.getElementById("notes");

const recordDots = ["", "bad", "warn", "bad", "info"];
const licenseStatus = [["Valid", "good"], ["Suspended", "warn"], ["Revoked", "bad"]];

let view = null;
let filled = false;
let searchMode = "name";
let entryType = 0;
let toastTimer = null;
let shownRecordId = null;
let tab = "records";
let reportsRequested = false;
let selectedReport = null;
let shownReportKey = null;

const narrative = document.getElementById("report-narrative");

const medical = () => Boolean(view?.medical);
const defaultType = () => (medical() ? 4 : 0);

function fineNote(entry) {
  return entry.fineStatus === 1 ? " unpaid" : entry.fineStatus === 2 ? " paid" : "";
}

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

function renderStop() {
  const stop = view.stop;
  document.getElementById("stop").hidden = !stop;
  if (!stop) return;

  const vehicle = stop.kind === "vehicle";
  setText("stop-title", vehicle ? `${stop.plate} · ${stop.model}` : "Person stop");
  setText("stop-sub", stop.name ? `${vehicle ? "Driver" : "Subject"}: ${stop.name}` : `${vehicle ? "Driver" : "Subject"} not identified yet`);

  const actions = [];
  if (vehicle && stop.ownerId) actions.push(button("Registered owner", "button small", { person: stop.ownerId }));
  else if (vehicle) {
    const run = button(stop.checking ? "Running…" : "Run plate", "button small primary", { field: "stopPlate" });
    run.disabled = stop.checking || stop.plateChecked;
    actions.push(run);
  }
  if (stop.characterId) actions.push(button(vehicle ? "Driver" : "Subject", "button small", { person: stop.characterId }));
  document.getElementById("stop-actions").replaceChildren(...actions);
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
    const meta = [formatDate(entry.createdAt), entry.officerCallsign, entry.fine > 0 ? `$${entry.fine}${fineNote(entry)}` : null].filter(Boolean).join(" · ");
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

function showTab(next) {
  tab = view?.medical ? next : "records";
  for (const node of document.querySelectorAll("[data-tab]")) node.classList.toggle("active", node.dataset.tab === tab);
  document.getElementById("records-view").hidden = tab !== "records";
  search.hidden = tab !== "records";
  document.getElementById("reports-view").hidden = tab !== "reports";

  if (tab === "reports" && !reportsRequested) {
    reportsRequested = true;
    FivePRS.action("reports");
  }
}

function dateTime(value) {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "" : date.toLocaleString([], { month: "short", day: "numeric", year: "numeric", hour: "2-digit", minute: "2-digit" });
}

function reportItem(report, source) {
  const key = `${source}:${report.id}`;
  const node = el("button", { className: `result report-item${selectedReport === key ? " selected" : ""}`, dataset: { report: key } }, [
    el("span", { className: "report-item-title", text: `${report.title} · #${report.callId}` }),
    el("span", { className: "report-item-meta", text: `${report.location} · ${formatDate(report.createdAt)}` }),
  ]);
  node.type = "button";
  return node;
}

function findReport(key) {
  if (!key) return null;
  const [source, id] = key.split(":");
  const list = source === "draft" ? view.reports?.drafts : view.reports?.results;
  return (list ?? []).find((report) => report.id === Number(id)) ?? null;
}

function fact(label, value) {
  return el("div", {}, [el("dt", { className: "eyebrow", text: label }), el("dd", {}, [value])]);
}

function renderReport(report) {
  const draft = report.status === 0;
  setText("report-kind", report.kind === 0 ? "Patient care report" : "Incident report");
  setText("report-title", report.title);
  setText("report-meta", `Call #${report.callId} · ${dateTime(report.createdAt)}`);

  const status = document.getElementById("report-status");
  status.className = `pill ${draft ? "warn" : "good"}`;
  status.textContent = draft ? "Draft" : "Filed";

  const facts = [fact("Location", report.location), fact("Crew", report.crew || report.authorCallsign)];
  if (report.characterId) {
    facts.push(fact(report.kind === 0 ? "Patient" : "Person", button(report.personName ?? "Open record", "button small", { person: report.characterId })));
  } else if (report.kind === 0) {
    facts.push(fact("Patient", "Not identified"));
  }
  if (report.plate) facts.push(fact("Plate", el("span", { className: "mono", text: report.plate })));
  if (!draft) facts.push(fact("Filed", `${report.filedBy ?? "—"} · ${dateTime(report.filedAt)}`));
  document.getElementById("report-facts").replaceChildren(...facts);

  document.getElementById("report-editor").hidden = !draft;
  document.getElementById("report-actions").hidden = !draft;
  document.getElementById("report-text").hidden = draft;
  setText("report-text", report.narrative);
  setText("report-note", draft ? `Drafts your crew doesn't file are filed automatically ${view.autoFileMinutes} minutes after the call.` : "");

  if (shownReportKey !== selectedReport) {
    shownReportKey = selectedReport;
    narrative.value = report.narrative;
    narrative.maxLength = view.maxNarrative;
  }
  document.getElementById("report-save").disabled = false;
  document.getElementById("report-file").disabled = false;
}

function renderReports() {
  const reports = view.reports ?? { drafts: [] };
  const drafts = reports.drafts ?? [];
  setText("draft-count", drafts.length || "");
  document.getElementById("drafts").replaceChildren(...drafts.map((report) => reportItem(report, "draft")));
  document.getElementById("drafts-empty").hidden = drafts.length > 0;

  const results = reports.results;
  document.getElementById("filed").replaceChildren(...(results ?? []).map((report) => reportItem(report, "filed")));
  document.getElementById("filed-empty").hidden = !(results && results.length === 0);

  const report = findReport(selectedReport);
  if (!report) {
    selectedReport = null;
    shownReportKey = null;
  }
  document.getElementById("report-placeholder").hidden = Boolean(report);
  document.getElementById("report").hidden = !report;
  if (report) renderReport(report);
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
  const caution = medical() ? record.caution : null;
  document.getElementById("warrant").hidden = !(warrant || caution);
  setText("warrant-title", caution ? "CAUTION" : "ACTIVE WARRANT");
  if (caution) setText("warrant-detail", caution);
  else if (warrant) setText("warrant-detail", `${warrant.description} · ${warrant.officerCallsign || "on file"}, ${formatDate(warrant.createdAt)}`);

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
    setEntryType(defaultType());
  }
}

function render(next) {
  const modeChanged = Boolean(view?.medical) !== Boolean(next.medical);
  view = next;
  document.body.dataset.mode = next.medical ? "medical" : "police";
  setText("history-title", next.medical ? "Medical history" : "History");
  setText("history-empty", next.medical ? "No medical history on file." : "Nothing on file.");
  setText("entry-title", next.medical ? "New medical entry" : "New entry");
  setText("notes-text", next.medical ? "Treatment and notes" : "Charges and notes");
  if (filled && modeChanged) setEntryType(defaultType());

  if (!filled) {
    filled = true;
    fine.max = next.maxFine;
    notes.maxLength = next.maxDescription;
    renderOffences();
    setEntryType(defaultType());
  }

  showTab(tab);
  if (next.medical) renderReports();
  renderStop();
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
  document.getElementById("report-save").disabled = false;
  document.getElementById("report-file").disabled = false;
});

search.addEventListener("submit", (event) => {
  event.preventDefault();
  const value = term.value.trim();
  if (!value) return;
  showToast(null);
  if (searchMode === "plate") FivePRS.action("searchPlate", { plate: value });
  else FivePRS.action("searchName", { term: value });
});

document.getElementById("report-search").addEventListener("submit", (event) => {
  event.preventDefault();
  showToast(null);
  FivePRS.action("reportSearch", { term: document.getElementById("report-term").value.trim(), date: document.getElementById("report-date").value });
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

  if (target.dataset.tab) {
    showTab(target.dataset.tab);
  } else if (target.dataset.report) {
    selectedReport = target.dataset.report;
    renderReports();
  } else if (target.id === "report-save" || target.id === "report-file") {
    const report = findReport(selectedReport);
    if (!report) return;
    target.disabled = true;
    showToast(null);
    FivePRS.action(target.id === "report-save" ? "reportSave" : "reportFile", { reportId: report.id, narrative: narrative.value.trim() });
  } else if (target.dataset.mode) {
    setSearchMode(target.dataset.mode);
  } else if (target.dataset.field) {
    showToast(null);
    FivePRS.action(target.dataset.field);
  } else if (target.dataset.type) {
    setEntryType(Number(target.dataset.type));
  } else if (target.dataset.person) {
    showTab("records");
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
