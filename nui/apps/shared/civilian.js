let toastTimer = null;

function showToast(message) {
  const toast = document.getElementById("toast");
  toast.textContent = message ?? "";
  toast.hidden = !message;
  clearTimeout(toastTimer);
  toastTimer = message ? setTimeout(() => showToast(null), 6000) : null;
}

function confirmButton(button, label) {
  if (button.dataset.armed === "true") return true;
  button.dataset.armed = "true";
  button.textContent = label;
  return false;
}

function activeCharacter(view) {
  const state = view.state;
  return state?.characters.find((character) => character.id === state.activeCharacterId) ?? null;
}

function stateId(view, characterId) {
  return `${view.stateInfo?.abbreviation ?? "SA"}-${10000 + characterId}`;
}

function sexLetter(gender) {
  return String(gender ?? "").charAt(0).toUpperCase();
}

function renderCivilianFrame(view) {
  setDepartmentIcons(view.icons);
  const state = view.state;
  const active = activeCharacter(view);

  document.getElementById("loading").hidden = Boolean(state);
  const empty = document.getElementById("no-character");
  empty.dataset.text ??= empty.textContent;
  empty.textContent = state?.managedBy ? `Load a character in ${state.managedBy} and it shows up here.` : empty.dataset.text;
  empty.hidden = !state || Boolean(active);
  document.getElementById("content").hidden = !active;
  return active;
}

FivePRS.on("error", showToast);
