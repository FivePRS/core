const emergencyTab = document.getElementById("tab-emergency");
const emergencyForm = document.getElementById("em-form");

let emergencyView = null;
let selectedService = null;
let calling = false;

function showEmergencyError(message) {
  calling = false;
  const error = document.getElementById("em-error");
  error.textContent = message ?? "";
  error.hidden = !message;
  if (emergencyView) renderEmergencyForm();
}

function renderEmergencyForm() {
  const services = emergencyView.departments ?? [];
  if (!services.some((service) => service.id === selectedService)) selectedService = services[0]?.id ?? null;

  document.getElementById("em-services").replaceChildren(
    ...services.map((service) =>
      el("button", {
        className: `option${service.id === selectedService ? " selected" : ""}`,
        dataset: { service: service.id },
      }, [el("span", { className: "option-name", text: service.name })])
    )
  );

  const submit = document.getElementById("em-submit");
  submit.disabled = calling || selectedService === null;
  submit.textContent = calling ? "Calling…" : "Call 911";
}

function renderEmergency(view, reset) {
  emergencyView = view;
  emergencyForm.elements.description.maxLength = view.maxLength;

  const status = view.status;
  const hadCall = !document.getElementById("em-active").hidden;
  document.getElementById("em-active").hidden = !status;
  emergencyForm.hidden = Boolean(status);

  if (status) {
    calling = false;
    const service = (view.departments ?? []).find((department) => department.id === status.department);
    setText("em-call-id", `#${status.callId}`);
    setText("em-call-title", `${service?.name ?? "Emergency"} requested`);
    setText("em-call-description", status.description);
    setText("em-call-units", status.responding > 0
      ? `${status.responding} unit${status.responding === 1 ? "" : "s"} responding`
      : "Waiting for a unit to respond");
    emergencyTab.querySelector("[data-action=em-cancel]").disabled = false;
    return;
  }

  if (reset || hadCall) {
    emergencyForm.reset();
    showEmergencyError(null);
  }

  renderEmergencyForm();
}

emergencyForm.addEventListener("submit", (event) => {
  event.preventDefault();
  const data = new FormData(emergencyForm);
  showEmergencyError(null);
  calling = true;
  renderEmergencyForm();
  post("emCall", {
    departmentId: selectedService,
    description: String(data.get("description") ?? "").trim(),
    anonymous: data.get("anonymous") === "on",
  });
});

emergencyTab.addEventListener("click", (event) => {
  const target = event.target.closest("button");
  if (!target || target.disabled) return;

  if (target.dataset.service) {
    selectedService = Number(target.dataset.service);
    renderEmergencyForm();
  } else if (target.dataset.action === "em-cancel") {
    target.disabled = true;
    post("emCancel");
  }
});
