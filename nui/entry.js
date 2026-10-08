const entry = document.getElementById("entry");
const enterButton = document.getElementById("entry-enter");
let entryView = null;
let selectedDepartment = null;
let selectedAgency = null;
let entering = false;

function departmentById(id) {
  return entryView?.departments.find((department) => department.id === id) ?? null;
}

function selectDepartment(id) {
  const department = departmentById(id);
  selectedDepartment = department ? department.id : null;

  const agencies = department?.agencies ?? [];
  if (!agencies.some((agency) => agency.id === selectedAgency)) {
    const current = agencies.find((agency) => agency.id === entryView.agencyId);
    selectedAgency = (current ?? agencies[0])?.id ?? null;
  }
}

function optionButton(name, detail, selected, dataset) {
  return el("button", { className: `option${selected ? " selected" : ""}`, dataset }, [
    el("span", { className: "option-name", text: name }),
    detail ? el("span", { className: "option-detail", text: detail }) : null,
  ]);
}

function renderEntry() {
  const view = entryView;
  const returning = view.departmentId !== 0 || view.rank > 1 || view.xp > 0;

  document.getElementById("entry-title").textContent = returning
    ? `Welcome back, ${view.name}`
    : `Welcome to FivePRS, ${view.name}`;
  setText("entry-rank", view.rank);
  setText("entry-xp", `${view.xp} / ${view.xpToNext} XP`);
  document.getElementById("entry-xp-bar").style.width =
    `${Math.min(100, (view.xp / Math.max(1, view.xpToNext)) * 100).toFixed(1)}%`;

  const departments = view.departments ?? [];
  document.getElementById("entry-departments").replaceChildren(
    ...departments.map((department) =>
      optionButton(department.name, null, department.id === selectedDepartment, { department: department.id })
    )
  );

  const agencies = departmentById(selectedDepartment)?.agencies ?? [];
  document.getElementById("entry-agency-title").hidden = agencies.length === 0;
  document.getElementById("entry-agencies").replaceChildren(
    ...agencies.map((agency) =>
      optionButton(agency.name, agency.callsignPrefix, agency.id === selectedAgency, { agency: agency.id })
    )
  );

  document.getElementById("entry-empty").hidden = departments.length > 0;
  enterButton.disabled = entering || selectedDepartment === null;
  enterButton.textContent = entering ? "Going on duty…" : "Go on duty";
}

function showError(message) {
  const error = document.getElementById("entry-error");
  error.textContent = message ?? "";
  error.hidden = !message;
}

screens.entry = {
  open(payload) {
    entryView = payload;
    entering = false;
    selectedAgency = payload.agencyId || null;
    selectDepartment(departmentById(payload.departmentId) ? payload.departmentId : payload.departments[0]?.id);
    showError(null);
    entry.hidden = false;
    renderEntry();
  },
  rejected(message) {
    entering = false;
    showError(message);
    renderEntry();
  },
  close() {
    entry.hidden = true;
  },
};

entry.addEventListener("click", (event) => {
  const target = event.target.closest("button");
  if (!target || target.disabled) return;

  if (target.dataset.department) {
    selectDepartment(Number(target.dataset.department));
    showError(null);
    renderEntry();
  } else if (target.dataset.agency) {
    selectedAgency = target.dataset.agency;
    renderEntry();
  } else if (target.id === "entry-civilian") {
    post("entryClose");
  } else if (target.id === "entry-enter") {
    entering = true;
    showError(null);
    renderEntry();
    post("entryEnter", { departmentId: selectedDepartment, agencyId: selectedAgency ?? "" });
  }
});

document.addEventListener("keydown", (event) => {
  if (event.key === "Escape" && !entry.hidden && !entering) post("entryClose");
});
