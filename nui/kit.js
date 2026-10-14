function el(tag, options = {}, children = []) {
  const node = document.createElement(tag);
  if (options.className) node.className = options.className;
  if (options.text !== undefined && options.text !== null) node.textContent = String(options.text);
  if (options.dataset) Object.assign(node.dataset, options.dataset);
  if (options.disabled) node.disabled = true;
  for (const child of children) if (child) node.append(child);
  return node;
}

function setText(id, value) {
  document.getElementById(id).textContent = value ?? "—";
}

let departmentIcons = {};

function setDepartmentIcons(icons) {
  departmentIcons = icons ?? {};
}

function iconImage(url, className = "dept-icon") {
  if (!url) return null;
  const image = el("img", { className });
  image.alt = "";
  image.src = url;
  image.addEventListener("error", () => image.remove());
  return image;
}

function departmentIcon(department, className) {
  return iconImage(departmentIcons[String(department ?? "").toLowerCase()], className);
}

function setDepartmentIcon(id, department) {
  const icon = departmentIcon(department);
  document.getElementById(id).replaceChildren(...(icon ? [icon] : []));
}

const recordTypes = ["Citation", "Arrest", "Warning", "Warrant"];
const recordTones = ["enroute", "revoked", "busy", "revoked"];
const licenseLabels = ["Valid", "Suspended", "Revoked"];
const licenseTones = ["available", "enroute", "revoked"];

function formatDate(value) {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "" : date.toLocaleDateString([], { year: "numeric", month: "short", day: "numeric" });
}

function pill(label, tone) {
  return el("span", { className: `status status-${tone}`, text: label });
}

function age(dateOfBirth) {
  const born = new Date(`${dateOfBirth}T00:00:00`);
  const now = new Date();
  let years = now.getFullYear() - born.getFullYear();
  if (now.getMonth() < born.getMonth() || (now.getMonth() === born.getMonth() && now.getDate() < born.getDate())) years -= 1;
  return years;
}
