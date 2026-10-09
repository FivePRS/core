const config = window.FIVEPRS_LOADSCREEN ?? {};
const tips = Array.isArray(config.tips) ? config.tips : [];

const statusText = document.getElementById("status-text");
const statusPercent = document.getElementById("status-percent");
const progressBar = document.getElementById("progress-bar");
const tipElement = document.getElementById("tip");

let progress = 0;
let tipIndex = 0;

const serverName = document.getElementById("server-name");
serverName.textContent = config.serverName ?? "";
serverName.hidden = !config.serverName;

function setProgress(fraction) {
  const value = Math.max(progress, Math.min(1, fraction));
  progress = value;
  progressBar.style.width = `${(value * 100).toFixed(1)}%`;
  statusPercent.textContent = `${Math.round(value * 100)}%`;
}

function setStatus(text) {
  if (text) statusText.textContent = text;
}

function showTip() {
  if (tips.length === 0) return;
  tipElement.classList.add("fading");
  setTimeout(() => {
    tipElement.textContent = tips[tipIndex % tips.length];
    tipElement.classList.remove("fading");
    tipIndex += 1;
  }, 400);
}

function showWelcome() {
  const profile = window.nuiHandoverData?.fiveprs;
  if (!profile || !profile.name) return;

  document.getElementById("welcome-name").textContent = profile.name;

  const details = [profile.agency, profile.rank ? `Rank ${profile.rank}` : null].filter(Boolean);
  document.getElementById("welcome-detail").textContent =
    details.length > 0 ? details.join(" · ") : "Choose your department once you spawn.";

  document.getElementById("welcome").hidden = false;
}

const stages = {
  startInitFunctionOrder: "Initialising",
  startDataFileEntries: "Loading game data",
  performMapLoadFunction: "Loading map",
};

window.addEventListener("message", (event) => {
  const data = event.data ?? {};

  if (data.eventName === "loadProgress") {
    setProgress(data.loadFraction ?? 0);
  } else if (stages[data.eventName]) {
    setStatus(stages[data.eventName]);
  } else if (data.eventName === "onLogLine" && typeof data.message === "string") {
    setStatus(data.message.replace(/\.+$/, ""));
  }
});

function setupMusic() {
  const music = config.music ?? {};
  const tracks = (Array.isArray(music.tracks) ? music.tracks : []).filter(Boolean);
  if (tracks.length === 0) return;

  if (music.shuffle) {
    for (let i = tracks.length - 1; i > 0; i -= 1) {
      const j = Math.floor(Math.random() * (i + 1));
      [tracks[i], tracks[j]] = [tracks[j], tracks[i]];
    }
  }

  const audio = new Audio();
  audio.volume = Math.max(0, Math.min(1, Number(music.volume ?? 0.3)));
  let index = 0;

  const toggle = document.getElementById("music-toggle");
  const label = document.getElementById("music-label");
  const waves = document.getElementById("music-waves");
  const mutedIcon = document.getElementById("music-muted");

  let muted = false;
  try {
    muted = localStorage.getItem("fiveprs-loadscreen-muted") === "1";
  } catch {
    muted = false;
  }

  function render() {
    audio.muted = muted;
    label.textContent = muted ? "Music off" : "Music on";
    toggle.setAttribute("aria-pressed", String(muted));
    waves.hidden = muted;
    mutedIcon.hidden = !muted;
  }

  function play() {
    audio.play().catch(() => {
      document.addEventListener("click", () => audio.play().catch(() => {}), { once: true });
    });
  }

  function load(i) {
    index = i % tracks.length;
    audio.src = tracks[index];
    play();
  }

  let failures = 0;
  audio.addEventListener("playing", () => {
    failures = 0;
  });
  audio.addEventListener("ended", () => load(index + 1));
  audio.addEventListener("error", () => {
    failures += 1;
    if (failures < tracks.length) {
      load(index + 1);
    } else {
      toggle.hidden = true;
    }
  });

  toggle.addEventListener("click", (event) => {
    event.stopPropagation();
    muted = !muted;
    try {
      localStorage.setItem("fiveprs-loadscreen-muted", muted ? "1" : "0");
    } catch {}
    render();
    if (!muted && audio.paused) play();
  });

  toggle.hidden = false;
  render();
  load(0);
}

showWelcome();
showTip();
setInterval(showTip, 7000);
setupMusic();
