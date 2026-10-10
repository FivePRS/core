const settingsTab = document.getElementById("tab-settings");
const wallpaperForm = document.getElementById("wallpaper-form");
const wallpaperUrl = document.getElementById("wallpaper-url");

function selectedWallpaper(terminal) {
  return terminal.selected || `id:${terminal.default}`;
}

function wallpaperTile(option, selected) {
  const thumb = iconImage(option.url, "wallpaper-thumb");
  const tile = el("button", { className: `wallpaper-tile${selected ? " selected" : ""}`, dataset: { wallpaper: `id:${option.id}` } }, [
    el("span", { className: "wallpaper-preview" }, thumb ? [thumb] : []),
    el("span", { className: "wallpaper-label", text: option.label }),
  ]);
  tile.type = "button";
  return tile;
}

function renderDutySettings(unit) {
  document.getElementById("settings-duty").hidden = !unit;
  if (!unit) return;

  for (const button of settingsTab.querySelectorAll("[data-ai]")) {
    button.classList.toggle("active", button.dataset.ai === String(unit.aiCallouts));
    button.disabled = false;
  }
}

function renderSettings(terminal, unit) {
  renderDutySettings(unit);
  if (!terminal) return;

  const selected = selectedWallpaper(terminal);
  document.getElementById("wallpaper-grid").replaceChildren(
    ...terminal.wallpapers.map((option) => wallpaperTile(option, selected === `id:${option.id}`))
  );

  wallpaperForm.hidden = !terminal.allowCustom;
  document.getElementById("wallpaper-hint").hidden = !terminal.allowCustom;

  if (selected.startsWith("url:") && document.activeElement !== wallpaperUrl) {
    wallpaperUrl.value = selected.slice(4);
  }
}

settingsTab.addEventListener("click", (event) => {
  const target = event.target.closest("button");
  if (!target || target.disabled) return;

  if (target.dataset.wallpaper) {
    post("setWallpaper", { value: target.dataset.wallpaper });
  } else if (target.dataset.ai) {
    for (const button of settingsTab.querySelectorAll("[data-ai]")) button.disabled = true;
    post("aiCallouts", { enabled: target.dataset.ai === "true" });
  }
});

wallpaperForm.addEventListener("submit", (event) => {
  event.preventDefault();
  const url = wallpaperUrl.value.trim();
  if (url) post("setWallpaper", { value: `url:${url}` });
});
