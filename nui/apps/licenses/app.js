const statusText = ["Valid", "Suspended", "Revoked"];
const statusTone = ["good", "warn", "bad"];
const lockPath = "M5 11h14v10H5z M8 11V8a4 4 0 0 1 8 0v3";

let view = null;
let selectedType = null;

function lockIcon() {
  const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
  svg.setAttribute("viewBox", "0 0 24 24");
  svg.setAttribute("aria-hidden", "true");
  const path = document.createElementNS("http://www.w3.org/2000/svg", "path");
  path.setAttribute("d", lockPath);
  svg.append(path);
  return svg;
}

function expiryText(license) {
  if (!license.expiresAt) return "No expiry";
  const expires = new Date(license.expiresAt);
  return expires < new Date() ? `Expired ${cardDate(license.expiresAt)}` : `Expires ${cardDate(license.expiresAt)}`;
}

function licenseRow(license) {
  const held = license.status !== null && license.status !== undefined;
  const mini = el("span", { className: `mini${held ? "" : " empty-slot"}${held && license.status !== 0 ? " faded" : ""}` });
  if (held) mini.style.setProperty("--card-color", license.card?.color || "#1d3a6e");
  else if (!license.selfService) mini.append(lockIcon());

  const detail = held
    ? el("span", { className: `license-status ${statusTone[license.status]}`, text: license.status === 0 ? `${statusText[0]}${license.class ? ` · Class ${license.class}` : ""}` : statusText[license.status] })
    : el("span", { className: "license-status", text: license.selfService ? "Not held" : "Issued by law enforcement" });

  const text = el("span", { className: "license-text" }, [el("span", { className: "license-name", text: license.name }), detail]);

  if (held) {
    const button = el("button", { className: `license${license.type === selectedType ? " selected" : ""}`, dataset: { select: license.type } }, [mini, text]);
    button.type = "button";
    return button;
  }

  const row = el("div", { className: "license" }, [mini, text]);
  if (license.selfService) {
    const apply = el("button", { className: "button small accent", text: "Apply", dataset: { apply: license.type } });
    apply.type = "button";
    row.append(apply);
  }
  return row;
}

function render(next) {
  view = next;
  const active = renderCivilianFrame(next);
  if (!active) return;

  const licenses = next.state.licenses ?? [];
  const held = licenses.filter((license) => license.status !== null && license.status !== undefined);
  if (!held.some((license) => license.type === selectedType)) selectedType = (held.find((license) => license.status === 0) ?? held[0])?.type ?? null;

  document.getElementById("licenses").replaceChildren(...licenses.map(licenseRow));

  const selected = held.find((license) => license.type === selectedType);
  document.getElementById("card-empty").hidden = Boolean(selected);
  document.getElementById("card-actions").hidden = !selected;
  document.getElementById("card-slot").replaceChildren(...(selected
    ? [licenseCard({ ...active, stateId: stateId(next, active.id), state: next.stateInfo, license: selected })]
    : []));

  if (selected) {
    document.getElementById("show").disabled = selected.status !== 0;
    setText("card-note", `${expiryText(selected)} · ${selected.card?.issuer || "Issued by the state"}`);
  }
}

FivePRS.on("state", render);

document.getElementById("licenses").addEventListener("click", (event) => {
  const apply = event.target.closest("[data-apply]");
  if (apply && !apply.disabled) {
    apply.disabled = true;
    FivePRS.action("apply", { type: apply.dataset.apply });
    return;
  }

  const select = event.target.closest("[data-select]");
  if (select) {
    selectedType = select.dataset.select;
    render(view);
  }
});

document.getElementById("show").addEventListener("click", () => {
  if (selectedType) FivePRS.action("show", { type: selectedType });
});
