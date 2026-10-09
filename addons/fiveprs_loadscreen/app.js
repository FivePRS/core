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

showWelcome();
showTip();
setInterval(showTip, 7000);
