const silhouettePath = "M19 6a9 9 0 1 1 0 18 9 9 0 0 1 0-18z M2 44c1-10 8-16 17-16s16 6 17 16z";

function silhouette(color = "#4a5564") {
  const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
  svg.setAttribute("viewBox", "0 0 38 44");
  svg.setAttribute("aria-hidden", "true");
  const path = document.createElementNS("http://www.w3.org/2000/svg", "path");
  path.setAttribute("d", silhouettePath);
  path.setAttribute("fill", color);
  svg.append(path);
  return svg;
}

function photoFrame(photo, className = "photo") {
  const frame = el("div", { className });
  frame.append(silhouette());
  if (photo) {
    const image = el("img");
    image.alt = "";
    image.src = photo;
    image.addEventListener("error", () => image.remove());
    frame.append(image);
  }
  return frame;
}

function cardDate(value) {
  const date = value ? new Date(value) : null;
  if (!date || Number.isNaN(date.getTime())) return "—";
  const pad = (number) => String(number).padStart(2, "0");
  return `${pad(date.getMonth() + 1)}/${pad(date.getDate())}/${date.getFullYear()}`;
}

function birthDate(value) {
  const [year, month, day] = String(value ?? "").split("-");
  return year && month && day ? `${month}/${day}/${year}` : "—";
}

function cardField(label, value, emphasis) {
  return el("span", { className: "idcard-field" }, [
    el("span", { className: "idcard-label", text: label }),
    el("span", { className: `idcard-value${emphasis ? " emphasis" : ""}`, text: value }),
  ]);
}

const cardStamps = { 0: ["VALID", "valid"], 1: ["SUSPENDED", "suspended"], 2: ["REVOKED", "revoked"] };

function licenseCard(card, options = {}) {
  const license = card.license;
  const look = license.card ?? {};
  const holder = card;
  const state = card.state ?? { name: "San Andreas", abbreviation: "SA" };

  const root = el("div", { className: `idcard${options.small ? " small" : ""}` });
  root.style.setProperty("--card-color", look.color || "#1d3a6e");
  root.style.setProperty("--card-accent", look.accent || "#f2c94c");
  if (look.image) root.style.setProperty("--card-image", `url("${String(look.image).replace(/["\\]/g, "")}")`);

  const head = el("div", { className: "idcard-head" }, [
    el("span", { className: "idcard-seal", text: state.abbreviation }),
    el("span", { className: "idcard-state", text: String(state.name).toUpperCase() }),
    el("span", { className: "idcard-title", text: look.title || String(license.name).toUpperCase() }),
  ]);

  const name = el("div", { className: "idcard-name" }, [
    el("span", { text: String(holder.lastName ?? "").toUpperCase() }),
    el("span", { text: String(holder.firstName ?? "").toUpperCase() }),
  ]);

  const fields = el("div", { className: "idcard-grid" }, [
    cardField("DOB", birthDate(holder.dateOfBirth), true),
    cardField(license.class ? "CLASS" : "ID", license.class || holder.stateId || "—"),
    cardField("SEX", String(holder.gender ?? "").charAt(0).toUpperCase() || "—"),
    cardField("ISS", cardDate(license.issuedAt)),
  ]);

  const body = el("div", { className: "idcard-body" }, [
    photoFrame(holder.photo, "photo idcard-photo"),
    el("div", { className: "idcard-info" }, [
      el("div", { className: "idcard-number" }, [
        el("span", { className: "idcard-label", text: "NO" }),
        el("span", { className: "idcard-value emphasis large", text: license.number }),
      ]),
      el("div", {}, [
        el("span", { className: "idcard-label", text: "EXP " }),
        el("span", { className: "idcard-value emphasis", text: license.expiresAt ? cardDate(license.expiresAt) : "NONE" }),
      ]),
      name,
      fields,
      el("span", { className: "idcard-sign", text: `${holder.firstName ?? ""} ${holder.lastName ?? ""}`.trim() }),
    ]),
  ]);

  const [stampText, stampTone] = cardStamps[license.status] ?? ["", ""];
  root.append(
    head,
    body,
    photoFrame(holder.photo, "photo idcard-ghost"),
    stampText ? el("span", { className: `idcard-stamp ${stampTone}`, text: stampText }) : null,
    el("span", { className: "idcard-issuer", text: look.issuer || "" }),
  );
  return root;
}
