const resource = typeof GetParentResourceName === "function" ? GetParentResourceName() : "fiveprs";

function post(name, data = {}) {
  return fetch(`https://${resource}/${name}`, {
    method: "POST",
    headers: { "Content-Type": "application/json; charset=UTF-8" },
    body: JSON.stringify(data),
  }).catch(() => {});
}

const screens = {};

window.addEventListener("message", (event) => {
  const { screen, type, payload } = event.data ?? {};
  screens[screen]?.[type]?.(payload);
});

screens.clipboard = {
  copy(text) {
    const area = document.createElement("textarea");
    area.value = text;
    document.body.append(area);
    area.select();
    document.execCommand("copy");
    area.remove();
  },
};
