const creatorRoot = document.getElementById("creator");
const creatorBody = document.getElementById("creator-body");
const creatorTabs = document.getElementById("creator-tabs");

const creatorTabLabels = { model: "Model", heritage: "Heritage", face: "Face", details: "Hair & details", clothing: "Clothing" };
const creatorTabFocus = { model: "body", heritage: "face", face: "face", details: "face", clothing: "body" };
const hairSlot = 2;
const faceSlot = 0;

let creatorAppearance = null;
let creatorOptions = null;
let creatorTab = "model";
let creatorControls = [];
let creatorTimer = null;

function creatorTabsFor() {
  return creatorOptions.freemode ? ["model", "heritage", "face", "details", "clothing"] : ["model", "clothing"];
}

function itemFor(list, slot, fallback) {
  let item = list.find((entry) => entry.slot === slot);
  if (!item) {
    item = { slot, drawable: fallback, texture: fallback };
    list.push(item);
  }
  return item;
}

function overlayFor(index) {
  let overlay = creatorAppearance.overlays.find((entry) => entry.index === index);
  if (!overlay) {
    overlay = { index, style: -1, opacity: 1, color: 0, secondColor: 0 };
    creatorAppearance.overlays.push(overlay);
  }
  return overlay;
}

function queueCreatorUpdate() {
  clearTimeout(creatorTimer);
  creatorTimer = setTimeout(() => post("creatorUpdate", { appearance: JSON.stringify(creatorAppearance) }), 80);
}

function rangeControl({ label, min, max, step = 1, get, set, format }) {
  const value = el("span", { className: "creator-value" });
  const input = el("input", { className: "creator-range" });
  input.type = "range";
  input.min = String(min);
  input.step = String(step);

  const control = { input, value, max, format: format ?? ((v) => String(v)) };

  const apply = (next) => {
    const limit = Number(input.max);
    const clamped = Math.min(limit, Math.max(min, next));
    set(clamped);
    input.value = String(clamped);
    value.textContent = control.format(clamped);
    queueCreatorUpdate();
  };

  input.addEventListener("input", () => apply(Number(input.value)));

  const stepper = (direction, text) => {
    const button = el("button", { className: "button small creator-step", text });
    button.type = "button";
    button.addEventListener("click", () => apply(Number(input.value) + direction * step));
    return button;
  };

  control.refresh = () => {
    const limit = typeof max === "function" ? max(creatorOptions) : max;
    input.max = String(Math.max(min, limit));
    const current = Math.min(Number(input.max), Math.max(min, get()));
    input.value = String(current);
    value.textContent = control.format(current);
    const disabled = Number(input.max) <= min;
    input.disabled = disabled;
  };

  creatorControls.push(control);
  control.refresh();

  return el("div", { className: "creator-row" }, [
    el("div", { className: "creator-label" }, [el("span", { text: label }), value]),
    el("div", { className: "creator-input" }, [stepper(-1, "‹"), input, stepper(1, "›")]),
  ]);
}

function section(title, children) {
  return el("section", { className: "creator-section" }, [el("p", { className: "panel-title", text: title }), ...children]);
}

function percent(value) {
  return `${Math.round(value * 100)}%`;
}

function renderModelTab() {
  const options = creatorOptions.models.map((model) => {
    const button = el("button", {
      className: `option${model.model === creatorAppearance.model ? " selected" : ""}`,
      dataset: { model: model.model },
    }, [el("span", { className: "option-name", text: model.label })]);
    button.type = "button";
    return button;
  });

  const custom = options.slice(0, 2);
  const standard = options.slice(2);
  return [
    section("Custom character", [el("div", { className: "option-row" }, custom)]),
    standard.length ? section("Standard GTA ped", [el("div", { className: "option-grid creator-peds" }, standard)]) : null,
  ];
}

function renderHeritageTab() {
  const blend = creatorAppearance.headBlend;
  const parentName = (index) => creatorOptions.parents[index] ?? String(index);

  return [
    section("Parents", [
      rangeControl({
        label: "Mother", min: 0, max: creatorOptions.parents.length - 1, get: () => blend.shapeFirst,
        set: (v) => { blend.shapeFirst = v; blend.skinFirst = v; }, format: parentName,
      }),
      rangeControl({
        label: "Father", min: 0, max: creatorOptions.parents.length - 1, get: () => blend.shapeSecond,
        set: (v) => { blend.shapeSecond = v; blend.skinSecond = v; }, format: parentName,
      }),
    ]),
    section("Blend", [
      rangeControl({ label: "Resemblance", min: 0, max: 1, step: 0.01, get: () => blend.shapeMix, set: (v) => { blend.shapeMix = v; }, format: percent }),
      rangeControl({ label: "Skin tone", min: 0, max: 1, step: 0.01, get: () => blend.skinMix, set: (v) => { blend.skinMix = v; }, format: percent }),
    ]),
  ];
}

function renderFaceTab() {
  while (creatorAppearance.faceFeatures.length < creatorOptions.faceFeatures.length) creatorAppearance.faceFeatures.push(0);

  return [
    section("Features", creatorOptions.faceFeatures.map((name, index) =>
      rangeControl({
        label: name, min: -1, max: 1, step: 0.05,
        get: () => creatorAppearance.faceFeatures[index],
        set: (v) => { creatorAppearance.faceFeatures[index] = v; },
        format: (v) => v.toFixed(2),
      })
    )),
  ];
}

function componentMeta(slot) {
  return (options) => options.components.find((entry) => entry.slot === slot);
}

function propMeta(slot) {
  return (options) => options.props.find((entry) => entry.slot === slot);
}

function clothingControls(name, meta, item, minimum) {
  return [
    rangeControl({
      label: name, min: minimum, max: (options) => meta(options).drawables - 1,
      get: () => item.drawable, set: (v) => { item.drawable = v; item.texture = minimum < 0 && v < 0 ? -1 : 0; },
      format: (v) => (v < 0 ? "None" : String(v)),
    }),
    rangeControl({
      label: `${name} variant`, min: 0, max: (options) => meta(options).textures - 1,
      get: () => Math.max(0, item.texture), set: (v) => { item.texture = v; },
    }),
  ];
}

function renderDetailsTab() {
  const hair = itemFor(creatorAppearance.components, hairSlot, 0);
  const colorMax = creatorOptions.hairColors - 1;

  const overlays = creatorOptions.overlays.map((meta) => {
    const overlay = overlayFor(meta.index);
    const controls = [
      rangeControl({
        label: "Style", min: -1, max: (options) => options.overlays[meta.index].count - 1,
        get: () => overlay.style, set: (v) => { overlay.style = v; },
        format: (v) => (v < 0 ? "None" : String(v)),
      }),
      rangeControl({ label: "Opacity", min: 0, max: 1, step: 0.05, get: () => overlay.opacity, set: (v) => { overlay.opacity = v; }, format: percent }),
    ];
    if (meta.colorType) {
      controls.push(rangeControl({ label: "Colour", min: 0, max: colorMax, get: () => overlay.color, set: (v) => { overlay.color = v; } }));
    }
    return section(meta.name, controls);
  });

  return [
    section("Hair", [
      ...clothingControls("Style", componentMeta(hairSlot), hair, 0),
      rangeControl({ label: "Colour", min: 0, max: colorMax, get: () => creatorAppearance.hairColor, set: (v) => { creatorAppearance.hairColor = v; } }),
      rangeControl({ label: "Highlight", min: 0, max: colorMax, get: () => creatorAppearance.hairHighlight, set: (v) => { creatorAppearance.hairHighlight = v; } }),
    ]),
    section("Eyes", [
      rangeControl({ label: "Eye colour", min: 0, max: creatorOptions.eyeColors - 1, get: () => creatorAppearance.eyeColor, set: (v) => { creatorAppearance.eyeColor = v; } }),
    ]),
    ...overlays,
  ];
}

function renderClothingTab() {
  const skipped = creatorOptions.freemode ? [faceSlot, hairSlot] : [];
  const components = creatorOptions.components
    .filter((meta) => !skipped.includes(meta.slot) && meta.drawables > 0)
    .map((meta) => section(meta.name, clothingControls("Style", componentMeta(meta.slot), itemFor(creatorAppearance.components, meta.slot, 0), 0)));
  const props = creatorOptions.props
    .filter((meta) => meta.drawables > 0)
    .map((meta) => section(meta.name, clothingControls("Style", propMeta(meta.slot), itemFor(creatorAppearance.props, meta.slot, -1), -1)));

  return [...components, ...props];
}

function renderCreator() {
  const tabs = creatorTabsFor();
  if (!tabs.includes(creatorTab)) creatorTab = tabs[0];

  creatorTabs.replaceChildren(...tabs.map((tab) => {
    const button = el("button", { className: `tab-button${tab === creatorTab ? " active" : ""}`, text: creatorTabLabels[tab], dataset: { creatorTab: tab } });
    button.type = "button";
    return button;
  }));

  creatorControls = [];
  const builders = { model: renderModelTab, heritage: renderHeritageTab, face: renderFaceTab, details: renderDetailsTab, clothing: renderClothingTab };
  creatorBody.replaceChildren(...builders[creatorTab]().filter(Boolean));
  creatorBody.scrollTop = 0;
}

function setCreatorTab(tab) {
  creatorTab = tab;
  renderCreator();
  post("creatorCamera", { focus: creatorTabFocus[tab] ?? "body" });
}

creatorRoot.addEventListener("click", (event) => {
  const target = event.target.closest("button");
  if (!target || target.disabled) return;

  if (target.dataset.creatorTab) {
    setCreatorTab(target.dataset.creatorTab);
  } else if (target.dataset.model) {
    if (target.dataset.model === creatorAppearance.model) return;
    creatorAppearance.model = target.dataset.model;
    post("creatorUpdate", { appearance: JSON.stringify(creatorAppearance) });
  } else if (target.dataset.focus) {
    post("creatorCamera", { focus: target.dataset.focus });
  } else if (target.dataset.rotate) {
    post("creatorRotate", { delta: Number(target.dataset.rotate) });
  } else if (target.id === "creator-save") {
    clearTimeout(creatorTimer);
    post("creatorUpdate", { appearance: JSON.stringify(creatorAppearance) }).then(() => post("creatorSave"));
  } else if (target.id === "creator-cancel") {
    clearTimeout(creatorTimer);
    post("creatorCancel");
  }
});

screens.creator = {
  open(payload) {
    creatorAppearance = payload.appearance;
    creatorOptions = payload.options;
    creatorTab = "model";
    setText("creator-name", payload.name);
    creatorRoot.hidden = false;
    renderCreator();
  },
  reset(payload) {
    creatorAppearance = payload.appearance;
    creatorOptions = payload.options;
    renderCreator();
  },
  options(options) {
    creatorOptions = options;
    for (const control of creatorControls) control.refresh();
  },
  close() {
    clearTimeout(creatorTimer);
    creatorRoot.hidden = true;
  },
};
