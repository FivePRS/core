const customForm = document.getElementById("custom");
const customUrl = document.getElementById("custom-url");

let view = null;
let filled = false;
let saveTimer = null;

function setSwitch(node, on) {
  node.setAttribute("aria-checked", String(Boolean(on)));
}

function savePreferences(changes) {
  Object.assign(view.preferences, changes);
  clearTimeout(saveTimer);
  saveTimer = setTimeout(() => FivePRS.action("preferences", { ...view.preferences }), 150);
  renderPreferences();
}

function renderWallpapers() {
  document.getElementById("wallpapers").replaceChildren(...view.wallpapers.map((option) => {
    const image = el("img");
    image.alt = "";
    image.src = option.url;
    const node = el("button", { className: `wallpaper${option.value === view.wallpaper ? " selected" : ""}`, dataset: { wallpaper: option.value } }, [image, el("span", { text: option.label })]);
    node.type = "button";
    return node;
  }));

  customForm.hidden = !view.allowCustom;
  if (!filled && view.wallpaper.startsWith("url:")) customUrl.value = view.wallpaper.slice(4);
}

function renderPreferences() {
  const prefs = view.preferences;
  for (const node of document.querySelectorAll("[data-text-size]")) node.classList.toggle("active", node.dataset.textSize === prefs.textSize);
  for (const node of document.querySelectorAll("[data-toggle]")) setSwitch(node, prefs[node.dataset.toggle]);
  for (const node of document.querySelectorAll("[data-range]")) {
    if (document.activeElement !== node) node.value = prefs[node.dataset.range];
    document.querySelector(`[data-value="${node.dataset.range}"]`).textContent = `${prefs[node.dataset.range]}%`;
  }
}

function render(next) {
  view = next;
  renderWallpapers();
  renderPreferences();

  const ai = document.getElementById("ai");
  setSwitch(ai, next.aiCallouts);
  ai.disabled = !next.onDuty;
  document.getElementById("ai-note").hidden = next.onDuty;

  document.getElementById("bindings").replaceChildren(...next.bindings.map((binding) => el("div", { className: "binding" }, [
    el("span", { text: binding.label }),
    el("span", { className: `keycap${binding.key === "Not set" ? " unset" : ""}`, text: binding.key }),
  ])));

  const nameplate = document.getElementById("nameplate");
  if (next.nameplate && nameplate.getAttribute("src") !== next.nameplate) nameplate.src = next.nameplate;
  nameplate.hidden = !next.nameplate;
  document.getElementById("wordmark").hidden = Boolean(next.nameplate);
  setText("version", `FivePRS ${next.version}`);
  setText("channel", next.preRelease ? "Public test build. Report problems at github.com/FivePRS/core/issues" : "fiveprs.org");
  filled = true;
}

FivePRS.on("state", render);

document.getElementById("nameplate").addEventListener("error", (event) => {
  event.currentTarget.hidden = true;
  document.getElementById("wordmark").hidden = false;
});

customForm.addEventListener("submit", (event) => {
  event.preventDefault();
  const url = customUrl.value.trim();
  if (url) FivePRS.action("wallpaperUrl", { url });
});

for (const node of document.querySelectorAll("[data-range]")) {
  node.addEventListener("input", () => savePreferences({ [node.dataset.range]: Number(node.value) }));
}

document.addEventListener("click", (event) => {
  const target = event.target.closest("button");
  if (!target || target.disabled || !view) return;

  if (target.dataset.section) {
    for (const node of document.querySelectorAll("[data-section]")) node.classList.toggle("active", node === target);
    for (const name of ["appearance", "duty", "sound", "keys", "about"]) document.getElementById(`section-${name}`).hidden = name !== target.dataset.section;
  } else if (target.dataset.wallpaper) {
    FivePRS.action("wallpaper", { value: target.dataset.wallpaper });
  } else if (target.dataset.textSize) {
    savePreferences({ textSize: target.dataset.textSize });
  } else if (target.dataset.toggle) {
    savePreferences({ [target.dataset.toggle]: !view.preferences[target.dataset.toggle] });
  } else if (target.id === "ai") {
    FivePRS.action("aiCallouts", { enabled: !view.aiCallouts });
  }
});
