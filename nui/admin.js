const adminTab = document.getElementById("tab-admin");
const adminSearch = document.getElementById("admin-search");
const adminQuery = document.getElementById("admin-query");

const departmentNames = ["None", "Police", "EMS", "Fire"];

function shortLicense(license) {
  const id = license.replace(/^license:/, "");
  return id.length > 12 ? `${id.slice(0, 6)}…${id.slice(-4)}` : id;
}

function adminRow(player, departments) {
  const toggles = departments.map((department) => {
    const granted = player.departments.includes(department.id);
    return el("button", {
      className: `button small${granted ? " active" : ""}`,
      text: department.name,
      dataset: { license: player.license, department: department.id, granted: String(!granted) },
    });
  });

  return el("tr", {}, [
    el("td", {}, [
      el("span", { text: player.name }),
      el("span", {
        className: "sub mono muted",
        text: player.online ? `ID ${player.serverId} · ${shortLicense(player.license)}` : shortLicense(player.license),
      }),
    ]),
    el("td", { className: "mono", text: player.rank }),
    el("td", { className: "muted", text: departmentNames[player.department] ?? "None" }),
    el("td", {}, [el("div", { className: "actions admin-toggles" }, toggles)]),
  ]);
}

function renderAdmin(view, reset) {
  if (!view) return;

  const state = view.state;
  document.getElementById("admin-unrestricted").hidden = state.restrictDepartments;

  document.getElementById("admin-online").replaceChildren(...state.online.map((player) => adminRow(player, view.departments)));
  setText("admin-online-count", `${state.online.length} online`);

  const searched = Boolean(state.query && state.query.trim().length >= 2);
  document.getElementById("admin-results").replaceChildren(...state.results.map((player) => adminRow(player, view.departments)));
  document.getElementById("admin-results-empty").hidden = !searched || state.results.length > 0;

  if (reset) adminQuery.value = state.query ?? "";
}

adminSearch.addEventListener("submit", (event) => {
  event.preventDefault();
  post("adminSearch", { query: adminQuery.value.trim() });
});

adminTab.addEventListener("click", (event) => {
  const target = event.target.closest("button");
  if (!target || target.disabled || !target.dataset.license) return;

  target.disabled = true;
  post("adminRoster", {
    license: target.dataset.license,
    departmentId: Number(target.dataset.department),
    granted: target.dataset.granted === "true",
  });
});
