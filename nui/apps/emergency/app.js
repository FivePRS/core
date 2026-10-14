const form = document.getElementById("form");
const cancel = document.getElementById("cancel");
const description = form.elements.description;

const serviceDetails = {
  police: "Crime, danger, disputes",
  fire: "Fires, rescues, hazards",
  ems: "Injuries, medical help",
};

let view = null;
let selectedService = null;
let calling = false;
let toastTimer = null;

function showToast(message) {
  calling = false;
  const toast = document.getElementById("toast");
  toast.textContent = message ?? "";
  toast.hidden = !message;
  clearTimeout(toastTimer);
  toastTimer = message ? setTimeout(() => showToast(null), 6000) : null;
  if (view) renderForm();
}

function time(value) {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "" : date.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
}

function renderForm() {
  const services = view.departments ?? [];
  if (!services.some((service) => service.id === selectedService)) selectedService = services[0]?.id ?? null;

  document.getElementById("services").replaceChildren(...services.map((service) => {
    const button = el("button", { className: `service${service.id === selectedService ? " selected" : ""}`, dataset: { service: service.id } }, [
      departmentIcon(service.name),
      el("span", { className: "service-name", text: service.name }),
      el("span", { className: "service-detail", text: serviceDetails[service.name.toLowerCase()] ?? "" }),
    ]);
    button.type = "button";
    return button;
  }));

  const submit = document.getElementById("submit");
  submit.disabled = calling || selectedService === null;
  setText("submit-label", calling ? "Calling…" : "Call 911");
  setText("counter", `${description.value.length} / ${view.maxLength}`);
}

function step(title, detail, state) {
  return el("li", { className: `step ${state}` }, [
    el("span", { className: "step-mark" }),
    el("span", {}, [el("span", { className: "step-title", text: title }), detail ? el("span", { className: "step-detail", text: detail }) : null]),
  ]);
}

function renderStatus(status) {
  const service = (view.departments ?? []).find((department) => department.id === status.department);
  setText("call-id", `#${status.callId} · ${service?.name ?? "Emergency"}`);
  setText("call-description", `“${status.description}”`);

  const responding = status.responding > 0;
  document.getElementById("timeline").replaceChildren(
    step("Call received", time(status.receivedAt), "done"),
    step("Units responding", responding ? `${(status.units ?? []).join(", ")} en route` : "Waiting for a unit", responding ? "done" : "current"),
    step("On scene", status.onScene ? "A unit has arrived" : responding ? "Units are on the way" : "", status.onScene ? "done" : responding ? "current" : ""),
    step("Cleared", "", ""),
  );
  cancel.disabled = false;
}

function render(next) {
  const hadCall = view?.status != null;
  view = next;
  setDepartmentIcons(next.icons);
  description.maxLength = next.maxLength;
  setText("location", next.location || "your current position");

  const status = next.status;
  document.getElementById("active").hidden = !status;
  document.getElementById("idle").hidden = Boolean(status);

  if (status) calling = false;
  else if (hadCall) {
    form.reset();
    showToast(null);
  }

  renderForm();
  form.querySelectorAll("button, textarea, input").forEach((node) => {
    if (status) node.disabled = true;
    else if (node.id !== "submit") node.disabled = false;
  });
  if (status) renderStatus(status);
}

FivePRS.on("state", render);
FivePRS.on("error", showToast);

description.addEventListener("input", () => {
  if (view) setText("counter", `${description.value.length} / ${view.maxLength}`);
});

form.addEventListener("submit", (event) => {
  event.preventDefault();
  const data = new FormData(form);
  showToast(null);
  calling = true;
  renderForm();
  FivePRS.action("call", {
    departmentId: selectedService,
    description: String(data.get("description") ?? "").trim(),
    anonymous: data.get("anonymous") === "on",
  });
});

document.getElementById("services").addEventListener("click", (event) => {
  const target = event.target.closest("[data-service]");
  if (!target || target.disabled) return;
  selectedService = Number(target.dataset.service);
  renderForm();
});

cancel.addEventListener("click", () => {
  cancel.disabled = true;
  FivePRS.action("cancel");
});
