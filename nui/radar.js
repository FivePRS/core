const radar = document.getElementById("radar");
const lidar = document.getElementById("lidar");
const remote = document.getElementById("radar-remote");
const remoteBody = document.getElementById("radar-remote-body");

const flagLabels = {
  stolen: "Stolen",
  warrant: "Warrant",
  unregistered: "Unregistered",
  suspended: "Suspended license",
};

const radarState = { operator: null, last: null, dragging: null, beep: 70 };

function placeAboveMinimap(panel, anchor) {
  if (!anchor) return;
  panel.style.top = "auto";
  panel.style.left = `${(anchor.left * 100).toFixed(2)}vw`;
  panel.style.bottom = anchor.visible ? `calc(${((1 - anchor.top) * 100).toFixed(2)}vh + 0.8vh)` : `${((1 - anchor.top - 0.18) * 100).toFixed(2)}vh`;
  panel.style.width = `max(${(anchor.width * 100).toFixed(2)}vw, 18rem)`;
}

function placeRadar(state) {
  const layout = radarState.operator ?? state.layout ?? {};
  const scale = (layout.scale ?? 100) / 100;
  if (layout.x === null || layout.x === undefined || layout.y === null || layout.y === undefined) {
    placeAboveMinimap(radar, state.anchor);
    radar.style.transformOrigin = "bottom left";
  } else {
    radar.style.bottom = "auto";
    radar.style.left = `${(layout.x * 100).toFixed(2)}vw`;
    radar.style.top = `${(layout.y * 100).toFixed(2)}vh`;
    radar.style.transformOrigin = "top left";
  }
  radar.style.transform = scale === 1 ? "" : `scale(${scale})`;
}

function segments(value, width = 3) {
  if (value === null || value === undefined) return "!".repeat(width);
  return String(value).slice(-width).padStart(width, "!");
}

function antennaMarkup(label) {
  const pane = (kind, text) =>
    el("div", { className: `radar-window ${kind}` }, [
      el("span", { className: "radar-window-label", text }),
      el("span", { className: `radar-seg ${kind}` }, [el("i", { text: "888" }), el("b", { dataset: { value: kind } })]),
    ]);

  return [
    el("header", { className: "radar-antenna-head" }, [
      el("span", { className: "radar-antenna-name", text: label }),
      el("span", { className: "radar-led xmit", text: "XMIT" }),
      el("span", { className: "radar-led mode", dataset: { mode: "" } }),
      el("span", { className: "radar-led lock", text: "LOCK" }),
      el("span", { className: "radar-direction" }),
    ]),
    el("div", { className: "radar-windows" }, [pane("target", "Target"), pane("fast", "Fast")]),
  ];
}

const antennas = {
  front: document.getElementById("radar-front"),
  rear: document.getElementById("radar-rear"),
};
antennas.front.replaceChildren(...antennaMarkup("Front"));
antennas.rear.replaceChildren(...antennaMarkup("Rear"));

function renderAntenna(section, antenna) {
  const on = Boolean(antenna?.xmit);
  section.classList.toggle("off", !on);
  section.classList.toggle("locked", Boolean(antenna?.locked));
  section.classList.toggle("alert", Boolean(antenna?.alert));
  section.classList.toggle("error", Boolean(antenna?.error));
  section.querySelector(".radar-led.xmit").classList.toggle("lit", on);
  section.querySelector(".radar-led.lock").classList.toggle("lit", Boolean(antenna?.locked));
  section.querySelector(".radar-led.mode").textContent = antenna?.mode === "opp" ? "OPP" : "SAME";

  const direction = section.querySelector(".radar-direction");
  direction.textContent = on && antenna?.target !== null && antenna?.target !== undefined ? (antenna.direction === "approach" ? "▲ Closing" : "▼ Away") : "";
  direction.classList.toggle("closing", antenna?.direction === "approach");

  const target = section.querySelector('[data-value="target"]');
  const fast = section.querySelector('[data-value="fast"]');
  if (!on) {
    target.textContent = segments(null);
    fast.textContent = segments(null);
  } else if (antenna.error) {
    target.textContent = "Err";
    fast.textContent = segments(null);
  } else {
    target.textContent = segments(antenna.target);
    fast.textContent = segments(antenna.fast);
  }
}

function plateLine(label, read) {
  if (!read) return [el("span", { className: "radar-plate-label", text: label }), el("span", { className: "radar-plate-empty", text: "No plate" })];

  const status = !read.checked
    ? [el("span", { className: "radar-flag pending", text: "Checking" })]
    : read.flags.length
      ? read.flags.map((flag) => el("span", { className: "radar-flag hit", text: flagLabels[flag] ?? flag }))
      : [el("span", { className: "radar-flag clear", text: "Clear" })];

  return [
    el("span", { className: "radar-plate-label", text: label }),
    el("span", { className: "radar-plate-number", text: read.plate }),
    el("span", { className: "radar-plate-model", text: read.model ?? "" }),
    ...status,
  ];
}

const radarAudio = { context: null, oscillator: null, gain: null };

function audioContext() {
  if (!radarAudio.context) radarAudio.context = new AudioContext();
  if (radarAudio.context.state === "suspended") radarAudio.context.resume();
  return radarAudio.context;
}

const soundPatterns = {
  lock: [[1250, 0, 0.07]],
  unlock: [[820, 0, 0.07]],
  fast: [[1500, 0, 0.09], [1500, 0.14, 0.09], [1500, 0.28, 0.09]],
  hit: [[880, 0, 0.12], [660, 0.16, 0.12], [880, 0.32, 0.12], [660, 0.48, 0.12]],
  lidar: [[2100, 0, 0.05], [2100, 0.08, 0.05]],
  miss: [[320, 0, 0.18]],
};

function playSound(kind, volume) {
  const pattern = soundPatterns[kind];
  if (!pattern || !volume) return;
  const context = audioContext();
  const level = (volume / 100) * 0.18;
  for (const [frequency, offset, duration] of pattern) {
    const oscillator = context.createOscillator();
    const gain = context.createGain();
    const start = context.currentTime + offset;
    oscillator.type = "square";
    oscillator.frequency.value = frequency;
    gain.gain.setValueAtTime(0, start);
    gain.gain.linearRampToValueAtTime(level, start + 0.005);
    gain.gain.setValueAtTime(level, start + duration - 0.01);
    gain.gain.linearRampToValueAtTime(0, start + duration);
    oscillator.connect(gain).connect(context.destination);
    oscillator.start(start);
    oscillator.stop(start + duration + 0.02);
  }
}

function dopplerSource(state) {
  for (const antenna of [state.front, state.rear]) {
    if (antenna?.xmit && !antenna.locked && !antenna.error && antenna.target) return antenna.target;
  }
  return null;
}

function updateDoppler(state) {
  const speed = state ? dopplerSource(state) : null;
  const volume = state?.audio?.doppler ?? 0;
  if (!radarAudio.oscillator) {
    if (speed === null || !volume) return;
    const context = audioContext();
    radarAudio.oscillator = context.createOscillator();
    radarAudio.gain = context.createGain();
    radarAudio.oscillator.type = "triangle";
    radarAudio.gain.gain.value = 0;
    radarAudio.oscillator.connect(radarAudio.gain).connect(context.destination);
    radarAudio.oscillator.start();
  }

  const now = radarAudio.context.currentTime;
  const level = speed === null ? 0 : (volume / 100) * 0.06;
  radarAudio.gain.gain.setTargetAtTime(level, now, 0.08);
  if (speed !== null) radarAudio.oscillator.frequency.setTargetAtTime(180 + speed * 9, now, 0.12);
}

screens.radar = {
  show(state) {
    radarState.last = state;
    radarState.beep = state.audio?.beep ?? radarState.beep;
    radar.hidden = false;
    if (!radarState.dragging) placeRadar(state);

    for (const node of radar.querySelectorAll(".radar-units")) node.textContent = state.units;
    setText("radar-patrol", segments(state.patrol));
    setText("radar-limit", state.fastLimit ? `${state.fastLock ? "Auto-lock" : "Alert"} ${state.fastLimit}` : "");
    renderAntenna(antennas.front, state.front);
    renderAntenna(antennas.rear, state.rear);
    radar.classList.toggle("alert", Boolean(state.front?.alert || state.rear?.alert));

    document.getElementById("radar-plates").hidden = !state.plateReader;
    const front = document.getElementById("radar-plate-front");
    const rear = document.getElementById("radar-plate-rear");
    front.replaceChildren(...plateLine("Front", state.plates?.front));
    rear.replaceChildren(...plateLine("Rear", state.plates?.rear));
    front.classList.toggle("hit", Boolean(state.plates?.front?.flags?.length));
    rear.classList.toggle("hit", Boolean(state.plates?.rear?.flags?.length));

    updateDoppler(state);
  },
  hide() {
    radar.hidden = true;
    radarState.last = null;
    updateDoppler(null);
    screens.radar.closeRemote();
  },
  sound(payload) {
    playSound(payload.kind, payload.volume);
  },
  remote(operator) {
    radarState.operator = operator;
    renderRemote();
    remote.hidden = false;
    radar.classList.add("editing");
  },
  closeRemote() {
    remote.hidden = true;
    radar.classList.remove("editing");
    radarState.operator = null;
    radarState.dragging = null;
  },
};

function saveOperator() {
  post("radarOperator", radarState.operator);
  if (radarState.last) placeRadar(radarState.last);
}

function toggleButton(label, active, onClick) {
  const button = el("button", { className: `radar-button${active ? " active" : ""}`, text: label });
  button.type = "button";
  button.addEventListener("click", onClick);
  return button;
}

function slider(label, value, min, max, step, onInput) {
  const input = el("input", { className: "radar-slider" });
  Object.assign(input, { type: "range", min, max, step, value });
  const output = el("span", { className: "radar-remote-value", text: value });
  input.addEventListener("input", () => {
    output.textContent = input.value;
    onInput(Number(input.value));
  });
  input.addEventListener("change", saveOperator);
  return el("label", { className: "radar-remote-row" }, [el("span", { className: "radar-remote-label", text: label }), input, output]);
}

function antennaControls(name, label) {
  const setup = radarState.operator[name];
  const update = (change) => {
    Object.assign(setup, change);
    saveOperator();
    renderRemote();
  };
  return el("div", { className: "radar-remote-row" }, [
    el("span", { className: "radar-remote-label", text: label }),
    toggleButton(setup.xmit ? "Transmitting" : "Hold", setup.xmit, () => update({ xmit: !setup.xmit })),
    toggleButton("Same", setup.mode === "same", () => update({ mode: "same" })),
    toggleButton("Opposite", setup.mode === "opp", () => update({ mode: "opp" })),
  ]);
}

function renderRemote() {
  const operator = radarState.operator;
  if (!operator) return;
  const units = radarState.last?.units ?? "";
  const changeLimit = (delta) => {
    operator.fastLimit = Math.max(0, Math.min(250, operator.fastLimit + delta));
    saveOperator();
    renderRemote();
  };

  remoteBody.replaceChildren(
    antennaControls("front", "Front antenna"),
    antennaControls("rear", "Rear antenna"),
    el("div", { className: "radar-remote-row" }, [
      el("span", { className: "radar-remote-label", text: "Fast limit" }),
      toggleButton("−5", false, () => changeLimit(-5)),
      el("span", { className: "radar-remote-value wide", text: operator.fastLimit ? `${operator.fastLimit} ${units}` : "Off" }),
      toggleButton("+5", false, () => changeLimit(5)),
      toggleButton("Auto-lock", operator.fastLock, () => {
        operator.fastLock = !operator.fastLock;
        saveOperator();
        renderRemote();
      }),
    ]),
    slider("Doppler volume", operator.doppler, 0, 100, 5, (value) => {
      operator.doppler = value;
      if (radarState.last) updateDoppler({ ...radarState.last, audio: { doppler: value } });
    }),
    slider("Beep volume", operator.beep, 0, 100, 5, (value) => {
      operator.beep = value;
    }),
    slider("Display size", operator.scale, 60, 160, 5, (value) => {
      operator.scale = value;
      if (radarState.last) placeRadar(radarState.last);
    }),
    el("div", { className: "radar-remote-row" }, [
      el("span", { className: "radar-remote-label", text: "Position" }),
      toggleButton("Above the minimap", operator.x === null || operator.x === undefined, () => {
        operator.x = null;
        operator.y = null;
        saveOperator();
        renderRemote();
      }),
      toggleButton("Test beep", false, () => playSound("lock", operator.beep)),
    ]),
  );
}

document.getElementById("radar-remote-done").addEventListener("click", () => post("radarRemoteClosed"));

window.addEventListener("keydown", (event) => {
  if (!remote.hidden && (event.key === "Escape" || event.key === "Backspace")) post("radarRemoteClosed");
});

radar.addEventListener("pointerdown", (event) => {
  if (!radarState.operator) return;
  const box = radar.getBoundingClientRect();
  radarState.dragging = { dx: event.clientX - box.left, dy: event.clientY - box.top };
  radar.setPointerCapture(event.pointerId);
});

radar.addEventListener("pointermove", (event) => {
  const drag = radarState.dragging;
  if (!drag || !radarState.operator) return;
  radarState.operator.x = Math.max(0, Math.min(0.95, (event.clientX - drag.dx) / window.innerWidth));
  radarState.operator.y = Math.max(0, Math.min(0.95, (event.clientY - drag.dy) / window.innerHeight));
  if (radarState.last) placeRadar(radarState.last);
});

radar.addEventListener("pointerup", () => {
  if (!radarState.dragging) return;
  radarState.dragging = null;
  saveOperator();
  renderRemote();
});

screens.lidar = {
  show(state) {
    lidar.hidden = false;
    placeAboveMinimap(lidar, state.anchor);
    lidar.classList.remove("miss");
    setText("lidar-units", state.units);
    setText("lidar-speed", "---");
    setText("lidar-distance", "---");
    setText("lidar-distance-units", "");
    setText("lidar-hint", "Aim and fire to measure");
  },
  reading(state) {
    lidar.classList.toggle("miss", Boolean(state.miss));
    playSound(state.miss ? "miss" : "lidar", radarState.beep);
    if (state.miss) {
      setText("lidar-hint", "No vehicle in your sights");
      return;
    }
    setText("lidar-hint", "Locked");
    setText("lidar-speed", String(state.speed));
    setText("lidar-distance", String(state.distance));
    setText("lidar-distance-units", state.distanceUnits);
  },
  hide() {
    lidar.hidden = true;
  },
};
