(() => {
  const listeners = {};
  const app = new URLSearchParams(location.search).get("app") ?? "";

  const send = (message) => parent.postMessage({ fiveprs: true, app, ...message }, "*");

  window.FivePRS = {
    app,
    context: null,
    on(type, handler) {
      (listeners[type] ??= []).push(handler);
      if (type === "context" && window.FivePRS.context !== null) handler(window.FivePRS.context);
    },
    action(name, data = {}) {
      send({ kind: "action", action: name, data });
    },
    home() {
      send({ kind: "home" });
    },
  };

  window.addEventListener("message", (event) => {
    const message = event.data;
    if (!message || message.fiveprs !== true || message.kind !== "event") return;
    if (message.type === "context") window.FivePRS.context = message.payload;
    for (const handler of listeners[message.type] ?? []) handler(message.payload);
  });

  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape") window.FivePRS.home();
  });

  send({ kind: "ready" });
})();
