const resource = typeof GetParentResourceName === "function" ? GetParentResourceName() : "fiveprs";

function post(name, data = {}) {
  return fetch(`https://${resource}/${name}`, {
    method: "POST",
    headers: { "Content-Type": "application/json; charset=UTF-8" },
    body: JSON.stringify(data),
  }).catch(() => {});
}

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

const screens = {};

window.addEventListener("message", (event) => {
  const { screen, type, payload } = event.data ?? {};
  screens[screen]?.[type]?.(payload);
});
