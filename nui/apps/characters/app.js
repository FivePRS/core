const form = document.getElementById("create");
const newButton = document.getElementById("new");
const photoUrlForm = document.getElementById("photo-url");

const photoWidth = 320;
const photoHeight = 400;
const maxPhotoLength = 115000;

let view = null;
let creating = false;
let pendingCount = null;
let reviewing = null;

function showCreate(open) {
  creating = open;
  form.hidden = !open;
  if (open) {
    form.reset();
    form.elements.firstName.focus();
  }
  if (view) render(view);
}

function characterButton(character, selected) {
  const status = selected ? "Active character" : `Last played ${formatDate(character.lastUsedAt)}`;
  const button = el("button", { className: `character${selected ? " selected" : ""}`, dataset: { character: character.id } }, [
    photoFrame(character.photo),
    el("span", { className: "character-text" }, [
      el("span", { className: "character-name", text: `${character.firstName} ${character.lastName}` }),
      el("span", { className: "character-meta", text: `${stateId(view, character.id)} · ${age(character.dateOfBirth)} · ${sexLetter(character.gender)}` }),
      el("span", { className: "character-status", text: status }),
    ]),
  ]);
  button.type = "button";
  return button;
}

const recordDots = ["", "bad", "warn", "bad"];

function recordEntry(entry) {
  const activeWarrant = entry.type === 3 && entry.active;
  const meta = [formatDate(entry.createdAt), entry.officerCallsign, entry.fine > 0 ? `$${entry.fine}` : null].filter(Boolean).join(" · ");
  return el("div", { className: "entry" }, [
    el("span", { className: `entry-dot ${recordDots[entry.type] ?? ""}` }),
    el("div", {}, [
      el("div", { className: "entry-head" }, [
        el("span", { className: "entry-type", text: activeWarrant ? "Active warrant" : recordTypes[entry.type] }),
        el("span", { className: "entry-meta", text: meta }),
      ]),
      el("p", { className: "entry-text", text: entry.description }),
      entry.resolution ? el("p", { className: "entry-meta", text: entry.resolution }) : null,
    ]),
  ]);
}

function renderTiles(state) {
  const held = (state.licenses ?? []).filter((license) => license.status === 0);
  setText("tile-licenses", held.length === 1 ? "1 license" : `${held.length} licenses`);
  setText("tile-licenses-detail", held.length ? held.map((license) => license.name.replace(/ License$| Permit$/, "")).join(", ") : "None held");

  const vehicles = state.vehicles ?? [];
  setText("tile-vehicles", vehicles.length === 1 ? "1 vehicle" : `${vehicles.length} vehicles`);
  setText("tile-vehicles-detail", vehicles.length ? `${vehicles[0].model} · ${vehicles[0].plate}` : "None registered");

  const records = state.records ?? [];
  const warrant = records.some((entry) => entry.type === 3 && entry.active);
  setText("tile-record", records.length ? `${records.length} on record` : "No records");
  setText("tile-record-detail", warrant ? "Active warrant" : records.length ? "See below" : "Nothing on file");
  document.getElementById("tile-record-icon").className = `tile-icon ${warrant ? "red" : "green"}`;

  const pillNode = document.getElementById("record-pill");
  pillNode.className = `pill ${warrant ? "bad" : records.length ? "warn" : "good"}`;
  pillNode.textContent = warrant ? "Active warrant" : records.length ? "Has a record" : "Clean record";
}

function cropMugshot(data) {
  return new Promise((resolve) => {
    const image = new Image();
    image.onload = () => {
      const height = image.naturalHeight * 0.62;
      const width = height * (photoWidth / photoHeight);
      const canvas = document.createElement("canvas");
      canvas.width = photoWidth;
      canvas.height = photoHeight;
      canvas.getContext("2d").drawImage(image, (image.naturalWidth - width) / 2, image.naturalHeight * 0.14, width, height, 0, 0, photoWidth, photoHeight);

      let quality = 0.85;
      let result = canvas.toDataURL("image/jpeg", quality);
      while (result.length > maxPhotoLength && quality > 0.4) {
        quality -= 0.1;
        result = canvas.toDataURL("image/jpeg", quality);
      }
      resolve(result);
    };
    image.onerror = () => resolve(null);
    image.src = data;
  });
}

async function renderReview(mugshot) {
  if (!mugshot) {
    reviewing = null;
    document.getElementById("review").hidden = true;
    return;
  }

  const key = `${mugshot.characterId}:${mugshot.data.length}`;
  if (reviewing?.key === key) return;

  const photo = await cropMugshot(mugshot.data);
  if (!photo) {
    showToast("That mugshot couldn't be read. Try again.");
    FivePRS.action("discardMugshot");
    return;
  }

  reviewing = { key, characterId: mugshot.characterId, photo };
  document.getElementById("review-image").src = photo;
  document.getElementById("review").hidden = false;
}

function render(next) {
  view = next;
  setDepartmentIcons(next.icons);

  const genders = form.elements.gender;
  if (genders.options.length === 0) {
    genders.replaceChildren(...next.genders.map((gender) => el("option", { text: gender })));
    form.elements.firstName.maxLength = next.maxNameLength;
    form.elements.lastName.maxLength = next.maxNameLength;
  }

  const state = next.state;
  document.getElementById("loading").hidden = Boolean(state);
  renderReview(next.mugshot);
  if (!state) return;

  if (pendingCount !== null && state.characters.length > pendingCount) {
    pendingCount = null;
    creating = false;
    form.hidden = true;
  }

  const active = activeCharacter(next);
  document.getElementById("characters").replaceChildren(...state.characters.map((character) => characterButton(character, character.id === state.activeCharacterId)));
  setText("slots", `${state.characters.length} / ${state.maxCharacters}`);
  const atLimit = state.characters.length >= state.maxCharacters;
  newButton.disabled = atLimit || creating;
  setText("new-label", atLimit ? "Character limit reached" : "New character");

  const showProfile = Boolean(active) && !creating;
  document.getElementById("no-character").hidden = Boolean(active) || creating;
  document.getElementById("content").hidden = !showProfile;
  if (!active) return;

  setText("citizen", `Citizen of ${next.stateInfo?.name ?? "San Andreas"}`);
  setText("name", `${active.firstName} ${active.lastName}`);
  setText("dob", birthDate(active.dateOfBirth));
  setText("age", age(active.dateOfBirth));
  setText("sex", active.gender);
  setText("state-id", stateId(next, active.id));
  document.getElementById("photo").replaceWith(Object.assign(photoFrame(active.photo, "photo board identity-photo"), { id: "photo" }));

  const mugshot = document.getElementById("mugshot");
  mugshot.disabled = !next.canMugshot;
  mugshot.title = next.canMugshot ? "" : "This server doesn't have screenshot-basic. Use an image link instead.";
  document.getElementById("photo-remove").hidden = !active.photo;

  const appearance = document.getElementById("appearance");
  appearance.disabled = Boolean(next.onDuty);
  document.getElementById("appearance-note").hidden = !next.onDuty;

  const remove = document.getElementById("delete");
  delete remove.dataset.armed;
  remove.disabled = false;
  remove.textContent = "Delete";

  renderTiles(state);
  const records = state.records ?? [];
  document.getElementById("records").replaceChildren(...records.map(recordEntry));
  document.getElementById("records-empty").hidden = records.length > 0;
}

FivePRS.on("state", render);
FivePRS.on("error", () => {
  pendingCount = null;
});

form.addEventListener("submit", (event) => {
  event.preventDefault();
  const data = new FormData(form);
  showToast(null);
  pendingCount = view?.state?.characters.length ?? 0;
  FivePRS.action("create", {
    firstName: String(data.get("firstName") ?? "").trim(),
    lastName: String(data.get("lastName") ?? "").trim(),
    dateOfBirth: String(data.get("dateOfBirth") ?? ""),
    gender: String(data.get("gender") ?? ""),
  });
});

newButton.addEventListener("click", () => showCreate(true));
document.getElementById("cancel").addEventListener("click", () => showCreate(false));

document.getElementById("characters").addEventListener("click", (event) => {
  const target = event.target.closest("[data-character]");
  if (!target) return;
  if (creating) showCreate(false);
  FivePRS.action("select", { characterId: Number(target.dataset.character) });
});

document.getElementById("appearance").addEventListener("click", () => FivePRS.action("appearance"));

document.getElementById("delete").addEventListener("click", (event) => {
  const button = event.currentTarget;
  if (!confirmButton(button, "Click again to delete")) return;
  button.disabled = true;
  FivePRS.action("delete", { characterId: view?.state?.activeCharacterId });
});

document.getElementById("mugshot").addEventListener("click", () => {
  FivePRS.action("mugshot", { characterId: view?.state?.activeCharacterId });
});

document.getElementById("photo-url-toggle").addEventListener("click", () => {
  photoUrlForm.hidden = !photoUrlForm.hidden;
  if (!photoUrlForm.hidden) photoUrlForm.elements.url.focus();
});

photoUrlForm.addEventListener("submit", (event) => {
  event.preventDefault();
  const url = photoUrlForm.elements.url.value.trim();
  if (!url) return;
  FivePRS.action("photoUrl", { characterId: view?.state?.activeCharacterId, url });
  photoUrlForm.hidden = true;
  photoUrlForm.reset();
});

document.getElementById("photo-remove").addEventListener("click", () => {
  FivePRS.action("removePhoto", { characterId: view?.state?.activeCharacterId });
  photoUrlForm.hidden = true;
});

document.getElementById("review-use").addEventListener("click", () => {
  if (!reviewing) return;
  fetch(`https://${FivePRS.resource}/fiveprsPhoto`, {
    method: "POST",
    headers: { "Content-Type": "application/json; charset=UTF-8" },
    body: JSON.stringify({ characterId: reviewing.characterId, photo: reviewing.photo }),
  }).catch(() => {});
  FivePRS.action("discardMugshot");
  document.getElementById("review").hidden = true;
});

document.getElementById("review-retake").addEventListener("click", () => {
  const characterId = reviewing?.characterId;
  FivePRS.action("discardMugshot");
  FivePRS.action("mugshot", { characterId });
});

document.getElementById("review-cancel").addEventListener("click", () => {
  FivePRS.action("discardMugshot");
  document.getElementById("review").hidden = true;
});
