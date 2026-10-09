const dutyTab = document.getElementById("tab-duty");
const dutyEnterButton = document.getElementById("duty-enter");
const callsignInput = document.getElementById("duty-callsign");

let dutyView = null;
let selectedDepartment = null;
let selectedAgency = null;
let entering = false;

function departmentById(id) {
  return dutyView?.departments.find((department) => department.id === id) ?? null;
}

function selectDepartment(id) {
  const department = departmentById(id);
  selectedDepartment = department ? department.id : null;

  const agencies = department?.agencies ?? [];
  if (!agencies.some((agency) => agency.id === selectedAgency)) {
    const current = agencies.find((agency) => agency.id === dutyView.agencyId);
    selectedAgency = (current ?? agencies[0])?.id ?? null;
  }
}

function optionButton(name, detail, selected, dataset) {
  return el("button", { className: `option${selected ? " selected" : ""}`, dataset }, [
    el("span", { className: "option-name", text: name }),
    detail ? el("span", { className: "option-detail", text: detail }) : null,
  ]);
}

function showDutyError(message) {
  entering = false;
  const error = document.getElementById("duty-error");
  error.textContent = message ?? "";
  error.hidden = !message;
  if (dutyView) renderDutyForm();
}

function renderDutyForm() {
  const departments = dutyView.departments ?? [];
  document.getElementById("duty-departments").replaceChildren(
    ...departments.map((department) =>
      optionButton(department.name, null, department.id === selectedDepartment, { department: department.id })
    )
  );

  const agencies = departmentById(selectedDepartment)?.agencies ?? [];
  const agency = agencies.find((item) => item.id === selectedAgency);
  callsignInput.placeholder = `${agency?.callsignPrefix || "UNIT"}-ID`;
  document.getElementById("duty-agency-title").hidden = agencies.length === 0;
  document.getElementById("duty-agencies").replaceChildren(
    ...agencies.map((item) =>
      optionButton(item.name, item.callsignPrefix, item.id === selectedAgency, { agency: item.id })
    )
  );

  document.getElementById("duty-empty").hidden = departments.length > 0;
  dutyEnterButton.disabled = entering || selectedDepartment === null;
  dutyEnterButton.textContent = entering ? "Going on duty…" : "Go on duty";
}

function renderDuty(view, reset) {
  dutyView = view;
  const returning = view.departmentId !== 0 || view.rank > 1 || view.xp > 0;

  document.getElementById("duty-title").textContent = returning
    ? `Welcome back, ${view.name}`
    : `Welcome to FivePRS, ${view.name}`;
  setText("duty-rank", view.rank);
  setText("duty-xp", `${view.xp} / ${view.xpToNext} XP`);
  document.getElementById("duty-xp-bar").style.width =
    `${Math.min(100, (view.xp / Math.max(1, view.xpToNext)) * 100).toFixed(1)}%`;

  document.getElementById("duty-off-form").hidden = view.isOnDuty;
  document.getElementById("duty-on-panel").hidden = !view.isOnDuty;

  if (view.isOnDuty) {
    setText("duty-unit-callsign", view.unit?.callsign ?? view.callsign);
    setText("duty-unit-agency", view.unit?.agency ?? "");
    document.getElementById("duty-off").disabled = false;
    return;
  }

  if (reset) {
    entering = false;
    selectedAgency = view.agencyId || null;
    selectDepartment(departmentById(view.departmentId) ? view.departmentId : view.departments[0]?.id);
    callsignInput.value = view.callsign ?? "";
    callsignInput.maxLength = view.callsignMaxLength ?? 12;
    showDutyError(null);
  } else if (selectedDepartment === null || !departmentById(selectedDepartment)) {
    selectDepartment(view.departments[0]?.id);
  }

  renderDutyForm();
}

dutyTab.addEventListener("click", (event) => {
  const target = event.target.closest("button");
  if (!target || target.disabled) return;

  if (target.dataset.department) {
    selectDepartment(Number(target.dataset.department));
    showDutyError(null);
  } else if (target.dataset.agency) {
    selectedAgency = target.dataset.agency;
    renderDutyForm();
  } else if (target.id === "duty-enter") {
    showDutyError(null);
    entering = true;
    renderDutyForm();
    post("dutyEnter", {
      departmentId: selectedDepartment,
      agencyId: selectedAgency ?? "",
      callsign: callsignInput.value.trim(),
    });
  } else if (target.id === "duty-off") {
    target.disabled = true;
    post("dutyOff");
  }
});

callsignInput.addEventListener("input", () => {
  const cleaned = callsignInput.value.toUpperCase().replace(/[^A-Z0-9-]/g, "");
  if (cleaned !== callsignInput.value) callsignInput.value = cleaned;
});

callsignInput.addEventListener("keydown", (event) => {
  if (event.key === "Enter" && !dutyEnterButton.disabled) dutyEnterButton.click();
});
