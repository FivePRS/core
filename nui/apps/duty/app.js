const enterButton = document.getElementById("enter");
const callsignInput = document.getElementById("callsign");

const startKey = "fiveprs.dutyStart";

const startOptions = [
  { id: "current", name: "As I am", detail: "Keep your look, no car" },
  { id: "here", name: "Here", detail: "Uniform, gear and car now" },
  { id: "teleport", name: "Station", detail: "Teleport and gear up" },
  { id: "drive", name: "Drive there", detail: "GPS route to collect gear" },
];

let view = null;
let filled = false;
let entering = false;
let toastTimer = null;
let selectedDepartment = null;
let selectedAgency = null;
let selectedStart = savedStart();
let selectedStation = null;

function savedStart() {
  try {
    const saved = localStorage.getItem(startKey);
    return startOptions.some((option) => option.id === saved) ? saved : "current";
  } catch {
    return "current";
  }
}

function needsStation(start) {
  return start === "teleport" || start === "drive";
}

function departmentById(id) {
  return view?.departments.find((department) => department.id === id) ?? null;
}

function selectDepartment(id) {
  const department = departmentById(id);
  selectedDepartment = department ? department.id : null;

  const agencies = department?.agencies ?? [];
  if (!agencies.some((agency) => agency.id === selectedAgency)) {
    const current = agencies.find((agency) => agency.id === view.agencyId);
    selectedAgency = (current ?? agencies[0])?.id ?? null;
  }
}

function availableStations() {
  return (departmentById(selectedDepartment)?.stations ?? []).filter(
    (station) => station.agencies.length === 0 || !selectedAgency || station.agencies.includes(selectedAgency)
  );
}

function formatDistance(meters) {
  const miles = meters / 1609.34;
  return miles < 0.1 ? "Nearby" : `${miles.toFixed(1)} mi`;
}

function choice(title, detail, selected, dataset, icon = null) {
  const button = el("button", { className: `choice${selected ? " selected" : ""}`, dataset }, [
    icon,
    el("span", {}, [el("span", { className: "choice-title", text: title }), detail ? el("span", { className: "choice-detail", text: detail }) : null]),
  ]);
  button.type = "button";
  return button;
}

function showToast(message) {
  entering = false;
  const toast = document.getElementById("toast");
  toast.textContent = message ?? "";
  toast.hidden = !message;
  clearTimeout(toastTimer);
  toastTimer = message ? setTimeout(() => showToast(null), 6000) : null;
  if (view) renderForm();
}

function renderStarts() {
  const stations = availableStations();
  const options = stations.length === 0 ? startOptions.filter((option) => !needsStation(option.id)) : startOptions;
  if (!options.some((option) => option.id === selectedStart)) selectedStart = "current";
  if (!stations.some((station) => station.id === selectedStation)) selectedStation = stations[0]?.id ?? null;

  document.getElementById("starts").replaceChildren(
    ...options.map((option) => choice(option.name, option.detail, option.id === selectedStart, { start: option.id }))
  );

  const list = document.getElementById("stations");
  list.hidden = !needsStation(selectedStart);
  list.replaceChildren(...stations.map((station) => {
    const button = el("button", { className: `station${station.id === selectedStation ? " selected" : ""}`, dataset: { station: station.id } }, [
      el("span", { text: station.name }),
      el("span", { className: "station-distance", text: formatDistance(station.distance) }),
    ]);
    button.type = "button";
    return button;
  }));
}

function renderForm() {
  const departments = view.departments ?? [];
  document.getElementById("departments").replaceChildren(
    ...departments.map((department) =>
      choice(department.name, null, department.id === selectedDepartment, { department: department.id }, departmentIcon(department.name))
    )
  );
  document.getElementById("no-departments").hidden = departments.length > 0;

  const agencies = departmentById(selectedDepartment)?.agencies ?? [];
  const agency = agencies.find((item) => item.id === selectedAgency);
  callsignInput.placeholder = `${agency?.callsignPrefix || "UNIT"}-ID`;
  document.getElementById("agency-step").hidden = agencies.length === 0;
  document.getElementById("agencies").replaceChildren(
    ...agencies.map((item) => choice(item.name, item.callsignPrefix, item.id === selectedAgency, { agency: item.id }))
  );

  renderStarts();

  const station = availableStations().find((item) => item.id === selectedStation);
  const callsign = callsignInput.value.trim() || callsignInput.placeholder;
  setText("summary", [agency?.callsignPrefix, callsign, needsStation(selectedStart) ? station?.name : startOptions.find((option) => option.id === selectedStart)?.name].filter(Boolean).join(" · "));

  enterButton.disabled = entering || selectedDepartment === null;
  enterButton.textContent = entering ? "Going on duty…" : "Go on duty";
}

function renderProfile() {
  const returning = view.departmentId !== 0 || view.rank > 1 || view.xp > 0;
  setText("greeting", returning ? `Welcome back, ${view.name}` : `Welcome to FivePRS, ${view.name}`);
  setText("rank-name", view.departmentId === 0 ? "Recruit" : view.rankName);
  setText("rank-detail", view.departmentId === 0 ? "Choose a department to start" : `${view.department} rank ${view.rank} of ${view.rankCount}`);

  document.getElementById("progress").hidden = !view.usesXp;
  const top = view.xpToNext === 0;
  setText("next-rank", top ? "Top rank" : `Next: ${view.nextRankName}`);
  setText("xp", top ? `${view.xp} XP` : `${view.xp} / ${view.xpToNext} XP`);
  document.getElementById("xp-bar").style.width = top ? "100%" : `${Math.min(100, (view.xp / view.xpToNext) * 100).toFixed(1)}%`;

  setText("stat-calls", view.stats?.calls ?? 0);
  setText("stat-arrests", view.stats?.arrests ?? 0);
  setText("stat-hours", view.stats?.hours ?? 0);
}

function render(next) {
  view = next;
  setDepartmentIcons(next.icons);
  renderProfile();

  document.getElementById("off-duty").hidden = view.isOnDuty;
  document.getElementById("on-duty").hidden = !view.isOnDuty;

  if (view.isOnDuty) {
    setText("unit-callsign", view.unit?.callsign ?? view.callsign);
    setText("unit-agency", view.unit?.agency ?? "");
    setDepartmentIcon("unit-icon", view.department);
    document.getElementById("off").disabled = false;
    return;
  }

  if (!filled) {
    filled = true;
    selectedAgency = view.agencyId || null;
    selectDepartment(departmentById(view.departmentId) ? view.departmentId : view.departments[0]?.id);
    callsignInput.value = view.callsign ?? "";
    callsignInput.maxLength = view.callsignMaxLength ?? 12;
  } else if (selectedDepartment === null || !departmentById(selectedDepartment)) {
    selectDepartment(view.departments[0]?.id);
  }

  renderForm();
}

FivePRS.on("state", render);
FivePRS.on("error", showToast);

document.addEventListener("click", (event) => {
  const target = event.target.closest("button");
  if (!target || target.disabled || !view) return;

  if (target.dataset.department) {
    selectDepartment(Number(target.dataset.department));
    showToast(null);
  } else if (target.dataset.agency) {
    selectedAgency = target.dataset.agency;
    renderForm();
  } else if (target.dataset.start) {
    selectedStart = target.dataset.start;
    try {
      localStorage.setItem(startKey, selectedStart);
    } catch {}
    renderForm();
  } else if (target.dataset.station) {
    selectedStation = target.dataset.station;
    renderForm();
  } else if (target === enterButton) {
    showToast(null);
    entering = true;
    renderForm();
    FivePRS.action("enter", {
      departmentId: selectedDepartment,
      agencyId: selectedAgency ?? "",
      callsign: callsignInput.value.trim(),
      start: selectedStart,
      stationId: selectedStation ?? "",
    });
  } else if (target.id === "off") {
    target.disabled = true;
    FivePRS.action("off");
  }
});

callsignInput.addEventListener("input", () => {
  const cleaned = callsignInput.value.toUpperCase().replace(/[^A-Z0-9-]/g, "");
  if (cleaned !== callsignInput.value) callsignInput.value = cleaned;
  if (view) renderForm();
});

callsignInput.addEventListener("keydown", (event) => {
  if (event.key === "Enter" && !enterButton.disabled) enterButton.click();
});
